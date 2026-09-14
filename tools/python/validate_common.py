"""out/normalized_csv/*.csv に対して、schema/tables/*.yaml の
validate.unique / validate.check_relation を検証する。

resolve_enum_ids.py の後(enumがidに変換された状態)に実行する想定。
検証エラーがあれば全件まとめて表示し、終了コード1で終了する。
"""

from __future__ import annotations

import csv
import sys
from pathlib import Path

from common import REPO_ROOT, load_config, load_yaml_dir


def read_table_csv(normalized_dir: Path, input_csv: str) -> list[dict[str, str]]:
    path = normalized_dir / f"{input_csv}.csv"
    if not path.exists():
        raise FileNotFoundError(f"正規化CSVが見つかりません: {path}(先にnormalize_csv.pyを実行してください)")
    with open(path, encoding="utf-8", newline="") as f:
        return list(csv.DictReader(f))


def validate_unique(table: dict, rows: list[dict[str, str]], errors: list[str]) -> None:
    for column in table.get("validate", {}).get("unique", []):
        seen: dict[str, int] = {}
        for sheet_row, row in enumerate(rows, start=5):
            value = row[column]
            if value in seen:
                errors.append(
                    f"{table['input_csv']}.csv: 列 '{column}' の値 '{value}' が"
                    f" {seen[value]}行目と{sheet_row}行目で重複しています"
                )
            else:
                seen[value] = sheet_row


def validate_check_relation(
    table: dict,
    rows: list[dict[str, str]],
    tables_by_name: dict[str, dict],
    rows_by_input_csv: dict[str, list[dict[str, str]]],
    errors: list[str],
) -> None:
    for relation in table.get("validate", {}).get("check_relation", []):
        field = relation["field"]
        target_table_name = relation["table"]
        target_field = relation["target_field"]

        target_table = tables_by_name.get(target_table_name)
        if target_table is None:
            errors.append(
                f"{table['input_csv']}.csv: check_relation の参照先テーブル"
                f" '{target_table_name}' が schema/tables に見つかりません"
            )
            continue

        target_rows = rows_by_input_csv[target_table["input_csv"]]
        target_values = {row[target_field] for row in target_rows}

        for sheet_row, row in enumerate(rows, start=5):
            value = row[field]
            if value not in target_values:
                errors.append(
                    f"{table['input_csv']}.csv {sheet_row}行目: 列 '{field}' の値 '{value}' は"
                    f" {target_table_name}.{target_field} に存在しません"
                )


def main() -> None:
    config = load_config()
    normalized_dir = REPO_ROOT / config["preprocessing"]["normalized_csv_dir"]
    tables_dir = REPO_ROOT / config["csharp_codegen"]["tables_dir"]

    tables = load_yaml_dir(tables_dir)
    tables_by_name = {table["name"]: table for table in tables}
    rows_by_input_csv = {
        table["input_csv"]: read_table_csv(normalized_dir, table["input_csv"])
        for table in tables
    }

    errors: list[str] = []
    for table in tables:
        rows = rows_by_input_csv[table["input_csv"]]
        validate_unique(table, rows, errors)
        validate_check_relation(table, rows, tables_by_name, rows_by_input_csv, errors)

    if errors:
        print("検証エラー:", file=sys.stderr)
        for error in errors:
            print(f"  - {error}", file=sys.stderr)
        sys.exit(1)

    print(f"検証OK: {len(tables)}テーブル")


if __name__ == "__main__":
    try:
        main()
    except FileNotFoundError as e:
        print(f"エラー: {e}", file=sys.stderr)
        sys.exit(1)
