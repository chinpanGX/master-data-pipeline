"""raw CSV(csv/*.csv)の先頭3行(1-2行目コメント + 3行目型情報)を除去し、
out/normalized_csv/{name}.csv に書き出す。

対象は schema/tables/*.yaml に input_csv が定義されているテーブルのみ。
input_csv に `*` を含む場合はglobとして展開し、一致した複数ファイル(水平分割)を
ファイル名の昇順で1つに結合する。出力ファイル名はglobから決められないため、
常にテーブル定義の name を使う(後続の resolve_enum_ids / validate / 各コンバーターも
name で読む)。
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


def resolve_input_csvs(csv_dir: Path, input_csv: str) -> list[Path]:
    """input_csv から読み込む raw CSV の一覧を返す。

    `*` を含まなければ従来どおり単一ファイル。含む場合はglobとして展開し、
    結合結果(=出力)が環境に依らず決定的になるようファイル名の昇順に並べる。
    """
    if "*" not in input_csv:
        src = csv_dir / f"{input_csv}.csv"
        if not src.exists():
            raise FileNotFoundError(f"raw CSVが見つかりません: {src}")
        return [src]

    srcs = sorted(csv_dir.glob(f"{input_csv}.csv"), key=lambda p: p.name)
    if not srcs:
        raise FileNotFoundError(f"raw CSVが見つかりません: {csv_dir / input_csv}.csv に一致するファイルがありません")
    return srcs


def read_raw_csv(src: Path) -> tuple[list[str], list[list[str]]]:
    """raw CSV 1ファイルを読み、(ヘッダー行, データ行) を返す。"""
    with open(src, encoding="utf-8-sig", newline="") as f:
        rows = list(csv.reader(f))

    if len(rows) <= ROWS_TO_STRIP:
        raise ValueError(f"{src}: ヘッダー行・データ行が見つかりません(行数不足)")

    header, *data = rows[ROWS_TO_STRIP:]
    return header, data


def normalize_table(csv_dir: Path, normalized_dir: Path, table: dict) -> list[Path]:
    srcs = resolve_input_csvs(csv_dir, table["input_csv"])

    header, rows = read_raw_csv(srcs[0])
    for src in srcs[1:]:
        other_header, other_rows = read_raw_csv(src)
        # 結合後は先頭ファイルのヘッダー1行で全行を読むため、列順まで一致して
        # いないと値が別の列に入ってしまう。分割ファイル間でヘッダーは完全一致を要求する
        if other_header != header:
            raise ValueError(
                f"{src.name}: ヘッダーが {srcs[0].name} と一致しません"
                f"(分割ファイルは同一ヘッダーにしてください)"
            )
        rows.extend(other_rows)

    dest = normalized_dir / f"{table['name']}.csv"
    with open(dest, "w", encoding="utf-8", newline="") as f:
        writer = csv.writer(f)
        writer.writerow(header)
        writer.writerows(rows)
    return srcs


def main() -> None:
    config = load_config()
    csv_dir = REPO_ROOT / config["preprocessing"]["csv_dir"]
    normalized_dir = REPO_ROOT / config["preprocessing"]["normalized_csv_dir"]
    tables_dir = REPO_ROOT / config["csharp_codegen"]["tables_dir"]

    tables = load_yaml_dir(tables_dir)
    clean_dir(normalized_dir)

    # globが広すぎて別テーブルのCSVまで拾うと、行が黙って二重に取り込まれるため検出する
    owner_by_src: dict[str, str] = {}
    for table in tables:
        srcs = normalize_table(csv_dir, normalized_dir, table)
        for src in srcs:
            owner = owner_by_src.setdefault(src.name, table["name"])
            if owner != table["name"]:
                raise ValueError(
                    f"{src.name} がテーブル '{owner}' と '{table['name']}' の両方の"
                    f" input_csv に一致しています(globを見直してください)"
                )
        print(f"normalized: {', '.join(src.name for src in srcs)} -> {table['name']}.csv")


if __name__ == "__main__":
    try:
        main()
    except (FileNotFoundError, ValueError) as e:
        print(f"エラー: {e}", file=sys.stderr)
        sys.exit(1)
