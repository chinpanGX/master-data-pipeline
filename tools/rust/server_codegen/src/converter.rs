//! out/normalized_csv/*.csv を out/server/*.json に変換する。
//! targets に "server" を含む列だけを抽出する(クライアント専用列は出力しない)。
//! enum列はresolve_enum_ids.pyによって既にidへ変換済みのため、そのまま数値として扱う。

use crate::schema::{ColumnDefinition, TableDefinition};
use serde_json::{Map, Value};
use std::path::Path;

/// bool表記ゆれの正規化(ハードコード)。
fn normalize_bool(raw: &str) -> Result<bool, String> {
    match raw {
        "true" | "TRUE" | "True" | "1" => Ok(true),
        "false" | "FALSE" | "False" | "0" => Ok(false),
        other => Err(format!("boolとして解釈できない値です: '{other}'")),
    }
}

fn field_to_json_value(field: &ColumnDefinition, raw: &str) -> Result<Value, String> {
    match field.type_.as_str() {
        "int" | "enum" => raw
            .parse::<i32>()
            .map(Value::from)
            .map_err(|_| format!("列 '{}' の値 '{raw}' はint(32bit)として解釈できません", field.name)),
        "long" => raw
            .parse::<i64>()
            .map(Value::from)
            .map_err(|_| format!("列 '{}' の値 '{raw}' はlong(64bit)として解釈できません", field.name)),
        "bool" => normalize_bool(raw)
            .map(Value::from)
            .map_err(|e| format!("列 '{}': {e}", field.name)),
        "string" => Ok(Value::from(raw)),
        other => Err(format!("未対応の型です: {other}")),
    }
}

fn convert_table(table: &TableDefinition, csv_dir: &Path) -> Result<Value, String> {
    let server_fields: Vec<&ColumnDefinition> =
        table.fields.iter().filter(|f| f.is_server_target()).collect();

    let csv_path = csv_dir.join(format!("{}.csv", table.input_csv));
    let mut reader = csv::Reader::from_path(&csv_path)
        .map_err(|e| format!("{}が見つかりません(先にnormalize_csv.py等を実行してください): {e}", csv_path.display()))?;

    let headers = reader
        .headers()
        .map_err(|e| format!("{}: {e}", csv_path.display()))?
        .clone();

    let field_indices: Vec<(usize, &ColumnDefinition)> = server_fields
        .iter()
        .map(|field| {
            headers
                .iter()
                .position(|h| h == field.name)
                .map(|idx| (idx, *field))
                .ok_or_else(|| format!("{}: 列 '{}' が見つかりません", csv_path.display(), field.name))
        })
        .collect::<Result<_, String>>()?;

    let mut rows = Vec::new();
    // 実際のスプレッドシート上の行番号(5行目〜データ)に合わせて表示する
    for (i, record) in reader.records().enumerate() {
        let record = record.map_err(|e| format!("{}: {e}", csv_path.display()))?;
        let sheet_row = i + 5;

        let mut obj = Map::new();
        for (idx, field) in &field_indices {
            let raw = record.get(*idx).unwrap_or("");
            let value = field_to_json_value(field, raw)
                .map_err(|e| format!("{}: {sheet_row}行目: {e}", csv_path.display()))?;
            obj.insert(field.name.clone(), value);
        }
        rows.push(Value::Object(obj));
    }

    Ok(Value::Array(rows))
}

/// 戻り値: (出力ファイル名, JSON値) のリスト。ファイル名は table.name を基準にする。
pub fn convert_all(tables: &[TableDefinition], csv_dir: &Path) -> Result<Vec<(String, Value)>, String> {
    tables
        .iter()
        .map(|table| {
            let value = convert_table(table, csv_dir)?;
            Ok((format!("{}.json", table.name), value))
        })
        .collect()
}
