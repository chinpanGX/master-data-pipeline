"""out/normalized_csv/*.csv のうち type: enum の列を、スプレッドシート入力値
(name)から schema/enums/*.yaml で定義された id に変換する(ファイルをin-place更新)。

normalize_csv.py の後、validate_common.py の前に実行する想定。
"""

from __future__ import annotations

import csv
import sys
from pathlib import Path

from common import REPO_ROOT, load_config, load_yaml_dir


def build_enum_maps(enums_dir: Path) -> dict[str, dict[str, int]]:
    enum_maps: dict[str, dict[str, int]] = {}
    for enum_def in load_yaml_dir(enums_dir):
        enum_maps[enum_def["enum"]] = {
            member["name"]: member["id"] for member in enum_def["members"]
        }
    return enum_maps


def resolve_table(
    normalized_dir: Path, table: dict, enum_maps: dict[str, dict[str, int]]
) -> None:
    enum_fields = {
        field["name"]: field["enum_type"]
        for field in table["fields"]
        if field["type"] == "enum"
    }
    if not enum_fields:
        return

    path = normalized_dir / f"{table['input_csv']}.csv"
    if not path.exists():
        raise FileNotFoundError(f"正規化CSVが見つかりません: {path}(先にnormalize_csv.pyを実行してください)")

    with open(path, encoding="utf-8", newline="") as f:
        reader = csv.DictReader(f)
        fieldnames = reader.fieldnames
        rows = list(reader)

    # 実際のスプレッドシート上の行番号(5行目〜データ)に合わせて表示する
    for sheet_row, row in enumerate(rows, start=5):
        for column, enum_type in enum_fields.items():
            if column not in row:
                continue
            name_to_id = enum_maps.get(enum_type)
            if name_to_id is None:
                raise ValueError(
                    f"{path.name}: 列 '{column}' の enum_type '{enum_type}' が"
                    f" schema/enums に見つかりません"
                )
            value = row[column]
            if value not in name_to_id:
                raise ValueError(
                    f"{path.name} {sheet_row}行目: 列 '{column}' の値 '{value}' は"
                    f" enum '{enum_type}' に定義されていません"
                )
            row[column] = str(name_to_id[value])

    with open(path, "w", encoding="utf-8", newline="") as f:
        writer = csv.DictWriter(f, fieldnames=fieldnames)
        writer.writeheader()
        writer.writerows(rows)


def main() -> None:
    config = load_config()
    normalized_dir = REPO_ROOT / config["preprocessing"]["normalized_csv_dir"]
    tables_dir = REPO_ROOT / config["csharp_codegen"]["tables_dir"]
    enums_dir = REPO_ROOT / config["csharp_codegen"]["enums_dir"]

    enum_maps = build_enum_maps(enums_dir)
    for table in load_yaml_dir(tables_dir):
        resolve_table(normalized_dir, table, enum_maps)
        print(f"resolved enums: {table['input_csv']}.csv")


if __name__ == "__main__":
    try:
        main()
    except (FileNotFoundError, ValueError) as e:
        print(f"エラー: {e}", file=sys.stderr)
        sys.exit(1)
