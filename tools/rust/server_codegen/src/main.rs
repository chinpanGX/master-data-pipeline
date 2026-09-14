mod converter;
mod generator;
mod schema;

use schema::{EnumDefinition, TableDefinition};
use std::fs;
use std::path::{Path, PathBuf};

fn main() {
    if let Err(e) = run() {
        eprintln!("エラー: {e}");
        std::process::exit(1);
    }
}

fn run() -> Result<(), String> {
    let repo_root = find_repo_root()?;
    let config = schema::load_config(&repo_root)?.server_codegen;

    let tables_dir = repo_root.join(&config.tables_dir);
    let enums_dir = repo_root.join(&config.enums_dir);
    let csv_dir = repo_root.join(&config.csv_dir);
    let output_rust_dir = repo_root.join(&config.output_rust_dir);
    let output_json_dir = repo_root.join(&config.output_json_dir);

    let tables: Vec<TableDefinition> = schema::load_yaml_dir(&tables_dir)?;
    let enums: Vec<EnumDefinition> = schema::load_yaml_dir(&enums_dir)?;

    clean_dir(&output_rust_dir)?;
    for (file_name, source) in generator::generate_all(&enums, &tables) {
        let path = output_rust_dir.join(&file_name);
        fs::write(&path, source).map_err(|e| format!("{}: {e}", path.display()))?;
        println!("generated: generated_rust/{file_name}");
    }

    clean_dir(&output_json_dir)?;
    for (file_name, value) in converter::convert_all(&tables, &csv_dir)? {
        let path = output_json_dir.join(&file_name);
        let json = serde_json::to_string_pretty(&value).map_err(|e| e.to_string())?;
        fs::write(&path, json).map_err(|e| format!("{}: {e}", path.display()))?;
        println!("converted: server/{file_name}");
    }

    Ok(())
}

/// 生成・変換直前に呼び出す共通クリーンアップ。
/// ディレクトリが存在すれば削除してから空の状態で再作成する。
fn clean_dir(path: &Path) -> Result<(), String> {
    if path.exists() {
        fs::remove_dir_all(path).map_err(|e| format!("{}: {e}", path.display()))?;
    }
    fs::create_dir_all(path).map_err(|e| format!("{}: {e}", path.display()))
}

/// 実行ファイルの場所から上に遡って config.yaml を探す。
fn find_repo_root() -> Result<PathBuf, String> {
    let exe_path = std::env::current_exe().map_err(|e| e.to_string())?;
    let mut dir = exe_path
        .parent()
        .ok_or_else(|| "実行ファイルの親ディレクトリが取得できません".to_string())?;

    loop {
        if dir.join("config.yaml").is_file() {
            return Ok(dir.to_path_buf());
        }
        dir = match dir.parent() {
            Some(parent) => parent,
            None => {
                return Err(format!(
                    "config.yaml が見つかりません(探索起点: {})",
                    exe_path.display()
                ))
            }
        };
    }
}
