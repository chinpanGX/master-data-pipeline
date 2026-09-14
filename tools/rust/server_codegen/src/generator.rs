//! schema/tables・schema/enums から Rust struct/enum のソースコードを組み立てる。
//! targets に "server" を含む列だけを抽出する(クライアント専用列は出力しない)。

use crate::schema::{ColumnDefinition, EnumDefinition, TableDefinition};

/// 生成される全Rustファイルの先頭に付与するDO NOT EDITコメント。
const GENERATED_FILE_HEADER: &str = "\
// DO NOT EDIT: このファイルは master-data-pipeline によって自動生成されています。
// 直接編集せず、スプレッドシートまたは schema/ 配下の定義を変更してください。

";

/// スキーマ上の snake_case な名前(テーブル名)を Rust の型名(PascalCase)に変換する。
pub fn to_pascal_case(snake_case: &str) -> String {
    snake_case
        .split('_')
        .filter(|part| !part.is_empty())
        .map(|part| {
            let mut chars = part.chars();
            match chars.next() {
                Some(first) => first.to_uppercase().collect::<String>() + chars.as_str(),
                None => String::new(),
            }
        })
        .collect()
}

/// enum名(PascalCase)をファイル名・モジュール名用の snake_case に変換する。
pub fn to_snake_case(pascal_case: &str) -> String {
    let mut result = String::new();
    for (i, c) in pascal_case.chars().enumerate() {
        if c.is_uppercase() {
            if i > 0 {
                result.push('_');
            }
            result.extend(c.to_lowercase());
        } else {
            result.push(c);
        }
    }
    result
}

/// type: int/string/bool/enum を Rust の型名に解決する。
fn resolve_rust_type(field: &ColumnDefinition) -> String {
    match field.type_.as_str() {
        "int" => "i64".to_string(),
        "string" => "String".to_string(),
        "bool" => "bool".to_string(),
        "enum" => field
            .enum_type
            .clone()
            .unwrap_or_else(|| panic!("type: enum のフィールドには enum_type の指定が必須です")),
        other => panic!("未対応の型です: {other}"),
    }
}

pub fn enum_file_name(enum_def: &EnumDefinition) -> String {
    to_snake_case(&enum_def.enum_name)
}

pub fn table_file_name(table: &TableDefinition) -> &str {
    &table.name
}

/// schema/enums/*.yaml 1件からenumの.rsソースを組み立てる。
pub fn generate_enum(enum_def: &EnumDefinition) -> String {
    let mut members = String::new();
    for member in &enum_def.members {
        members.push_str(&format!("    {} = {},\n", member.name, member.id));
    }

    let mut match_arms = String::new();
    for member in &enum_def.members {
        match_arms.push_str(&format!(
            "            {} => Ok({}::{}),\n",
            member.id, enum_def.enum_name, member.name
        ));
    }

    format!(
        "{header}\
use serde::{{Deserialize, Deserializer, Serialize, Serializer}};

#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum {name} {{
{members}}}

impl Serialize for {name} {{
    fn serialize<S>(&self, serializer: S) -> Result<S::Ok, S::Error>
    where
        S: Serializer,
    {{
        serializer.serialize_i64(*self as i64)
    }}
}}

impl<'de> Deserialize<'de> for {name} {{
    fn deserialize<D>(deserializer: D) -> Result<Self, D::Error>
    where
        D: Deserializer<'de>,
    {{
        let value = i64::deserialize(deserializer)?;
        match value {{
{match_arms}            other => Err(serde::de::Error::custom(format!(
                \"unknown {name} id: {{other}}\"
            ))),
        }}
    }}
}}
",
        header = GENERATED_FILE_HEADER,
        name = enum_def.enum_name,
        members = members,
        match_arms = match_arms,
    )
}

/// schema/tables/*.yaml 1件から、targetsに"server"を含む列だけを抽出したstructの.rsソースを組み立てる。
pub fn generate_struct(table: &TableDefinition) -> String {
    let server_fields: Vec<&ColumnDefinition> =
        table.fields.iter().filter(|f| f.is_server_target()).collect();

    let mut enum_types: Vec<&str> = server_fields
        .iter()
        .filter_map(|f| f.enum_type.as_deref())
        .collect();
    enum_types.sort_unstable();
    enum_types.dedup();

    let mut uses = String::new();
    for enum_type in &enum_types {
        uses.push_str(&format!(
            "use super::{}::{enum_type};\n",
            to_snake_case(enum_type)
        ));
    }
    if !uses.is_empty() {
        uses.push('\n');
    }

    let mut fields = String::new();
    for field in &server_fields {
        let rust_type = resolve_rust_type(field);
        fields.push_str(&format!("    pub {}: {},\n", field.name, rust_type));
    }

    format!(
        "{header}\
use serde::{{Deserialize, Serialize}};

{uses}\
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct {name} {{
{fields}}}
",
        header = GENERATED_FILE_HEADER,
        uses = uses,
        name = to_pascal_case(&table.name),
        fields = fields,
    )
}

/// 生成物一式をまとめて `pub mod`/`pub use` する mod.rs を組み立てる。
pub fn generate_mod_rs(enums: &[EnumDefinition], tables: &[TableDefinition]) -> String {
    let mut mods = String::new();
    let mut uses = String::new();

    for enum_def in enums {
        let file_name = enum_file_name(enum_def);
        mods.push_str(&format!("pub mod {file_name};\n"));
        uses.push_str(&format!(
            "pub use {file_name}::{name};\n",
            name = enum_def.enum_name
        ));
    }
    for table in tables {
        let file_name = table_file_name(table);
        mods.push_str(&format!("pub mod {file_name};\n"));
        uses.push_str(&format!(
            "pub use {file_name}::{name};\n",
            name = to_pascal_case(&table.name)
        ));
    }

    format!("{GENERATED_FILE_HEADER}{mods}\n{uses}")
}

/// schema/enums/*.yaml・schema/tables/*.yaml から out/generated_rust/*.rs 一式を生成する。
/// 出力先ディレクトリは呼び出し側で事前にクリーンアップ済みであること。
pub fn generate_all(enums: &[EnumDefinition], tables: &[TableDefinition]) -> Vec<(String, String)> {
    let mut files = Vec::new();

    for enum_def in enums {
        files.push((format!("{}.rs", enum_file_name(enum_def)), generate_enum(enum_def)));
    }

    for table in tables {
        files.push((format!("{}.rs", table_file_name(table)), generate_struct(table)));
    }

    files.push(("mod.rs".to_string(), generate_mod_rs(enums, tables)));

    files
}
