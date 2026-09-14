//! config.yaml / schema/tables/*.yaml / schema/enums/*.yaml の中間モデル。
//!
//! フォーマットの詳細は schema/tables/character.yaml のコメントを参照。

use serde::Deserialize;
use std::fs;
use std::path::Path;

#[derive(Debug, Deserialize)]
pub struct Config {
    pub server_codegen: ServerCodegenConfig,
}

#[derive(Debug, Deserialize)]
pub struct ServerCodegenConfig {
    pub tables_dir: String,
    pub enums_dir: String,
    pub csv_dir: String,
    pub output_rust_dir: String,
    pub output_json_dir: String,
}

/// schema/tables/*.yaml 1ファイル分。
#[derive(Debug, Deserialize)]
pub struct TableDefinition {
    pub name: String,
    pub input_csv: String,
    pub fields: Vec<ColumnDefinition>,
}

/// schema/tables/*.yaml の fields[] 1件分。セカンダリキーは無いため primary_key のみを持つ。
#[derive(Debug, Deserialize)]
pub struct ColumnDefinition {
    pub name: String,
    #[serde(rename = "type")]
    pub type_: String,
    pub enum_type: Option<String>,
    /// csharp-codegen側で[PrimaryKey]付与に使う情報。server_codegenでは現状未使用。
    #[serde(default)]
    #[allow(dead_code)]
    pub primary_key: bool,
    pub targets: Option<Vec<String>>,
}

impl ColumnDefinition {
    /// targets省略時は [client, server] がデフォルト。
    pub fn is_server_target(&self) -> bool {
        match &self.targets {
            Some(targets) if !targets.is_empty() => targets.iter().any(|t| t == "server"),
            _ => true,
        }
    }
}

/// schema/enums/*.yaml 1ファイル分。
#[derive(Debug, Deserialize)]
pub struct EnumDefinition {
    /// yaml上のキーは "enum"。Rust生成物のenum型名になる。
    #[serde(rename = "enum")]
    pub enum_name: String,
    pub members: Vec<EnumMemberDefinition>,
}

#[derive(Debug, Deserialize)]
pub struct EnumMemberDefinition {
    /// 出力される数値。並べ替えても変わらない固定値。
    pub id: i64,
    /// スプレッドシート上で入力される名前。
    pub name: String,
}

pub fn load_config(repo_root: &Path) -> Result<Config, String> {
    let path = repo_root.join("config.yaml");
    let text = fs::read_to_string(&path).map_err(|e| format!("{}: {e}", path.display()))?;
    serde_yaml::from_str(&text).map_err(|e| format!("{}: {e}", path.display()))
}

/// ディレクトリ直下の *.yaml をファイル名順にすべて読み込む。
pub fn load_yaml_dir<T: for<'de> Deserialize<'de>>(dir: &Path) -> Result<Vec<T>, String> {
    if !dir.is_dir() {
        return Err(format!("スキーマディレクトリが見つかりません: {}", dir.display()));
    }

    let mut paths: Vec<_> = fs::read_dir(dir)
        .map_err(|e| format!("{}: {e}", dir.display()))?
        .filter_map(|entry| entry.ok())
        .map(|entry| entry.path())
        .filter(|path| path.extension().is_some_and(|ext| ext == "yaml"))
        .collect();
    paths.sort();

    paths
        .into_iter()
        .map(|path| {
            let text = fs::read_to_string(&path).map_err(|e| format!("{}: {e}", path.display()))?;
            serde_yaml::from_str(&text).map_err(|e| format!("{}: {e}", path.display()))
        })
        .collect()
}
