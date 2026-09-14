"""raw CSV(csv/*.csv)の先頭3行(1-2行目コメント + 3行目型情報)を除去し、
out/normalized_csv/*.csv に書き出す。

対象は schema/tables/*.yaml に input_csv が定義されているテーブルのみ。
出力先は書き込み前にクリーンアップ(削除→再作成)し、古い正規化CSVが
残らないようにする。
"""

from __future__ import annotations

import csv
import sys
from pathlib import Path

from common import REPO_ROOT, clean_dir, load_config, load_yaml_dir

# 1-2行目: 自由記述コメント、3行目: 型情報行(コメント用)
ROWS_TO_STRIP = 3


def normalize_table(csv_dir: Path, normalized_dir: Path, input_csv: str) -> None:
    src = csv_dir / f"{input_csv}.csv"
    if not src.exists():
        raise FileNotFoundError(f"raw CSVが見つかりません: {src}")

    with open(src, encoding="utf-8-sig", newline="") as f:
        rows = list(csv.reader(f))

    if len(rows) <= ROWS_TO_STRIP:
        raise ValueError(f"{src}: ヘッダー行・データ行が見つかりません(行数不足)")

    normalized_rows = rows[ROWS_TO_STRIP:]

    dest = normalized_dir / f"{input_csv}.csv"
    with open(dest, "w", encoding="utf-8", newline="") as f:
        csv.writer(f).writerows(normalized_rows)


def main() -> None:
    config = load_config()
    csv_dir = REPO_ROOT / config["preprocessing"]["csv_dir"]
    normalized_dir = REPO_ROOT / config["preprocessing"]["normalized_csv_dir"]
    tables_dir = REPO_ROOT / config["csharp_codegen"]["tables_dir"]

    tables = load_yaml_dir(tables_dir)
    clean_dir(normalized_dir)

    for table in tables:
        normalize_table(csv_dir, normalized_dir, table["input_csv"])
        print(f"normalized: {table['input_csv']}.csv")


if __name__ == "__main__":
    try:
        main()
    except (FileNotFoundError, ValueError) as e:
        print(f"エラー: {e}", file=sys.stderr)
        sys.exit(1)
