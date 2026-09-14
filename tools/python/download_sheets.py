"""Google スプレッドシートから、schema/tables/*.yaml で定義された各テーブルに対応する
シート(タブ)をCSVとしてダウンロードし、csv/{input_csv}.csv に保存する。

シート(タブ)名は input_csv の値をそのまま使う(例: input_csv: character_master なら
スプレッドシート側にも "character_master" という名前のタブを用意する)。
"""

from __future__ import annotations

import csv
import sys
from pathlib import Path

from common import REPO_ROOT, clean_dir, load_config, load_yaml_dir
from google.oauth2 import service_account
from googleapiclient.discovery import build

SCOPES = ["https://www.googleapis.com/auth/spreadsheets.readonly"]


def build_sheets_service(credentials_path: Path):
    if not credentials_path.exists():
        raise FileNotFoundError(
            f"認証情報ファイルが見つかりません: {credentials_path}"
            "(README.mdのセットアップ手順を確認してください)"
        )

    credentials = service_account.Credentials.from_service_account_file(
        str(credentials_path), scopes=SCOPES
    )
    return build("sheets", "v4", credentials=credentials, cache_discovery=False)


def download_sheet(service, spreadsheet_id: str, sheet_name: str) -> list[list[str]]:
    result = (
        service.spreadsheets()
        .values()
        .get(spreadsheetId=spreadsheet_id, range=f"'{sheet_name}'")
        .execute()
    )
    values = result.get("values", [])
    if not values:
        raise ValueError(
            f"シート '{sheet_name}' にデータがありません"
            "(タブ名がinput_csvと一致しているか確認してください)"
        )
    return values


def write_csv(rows: list[list[str]], dest: Path) -> None:
    # 行によって取得セル数が異なる(末尾の空セルが省略される)ことがあるため、
    # 最大列数に合わせて空文字で埋めてから書き出す。
    width = max(len(row) for row in rows)
    with open(dest, "w", encoding="utf-8", newline="") as f:
        writer = csv.writer(f)
        for row in rows:
            writer.writerow(row + [""] * (width - len(row)))


def main() -> None:
    config = load_config()
    sheets_config = config.get("google_sheets")
    if not sheets_config:
        raise ValueError("config.yaml に google_sheets セクションがありません")

    tables_dir = REPO_ROOT / config["csharp_codegen"]["tables_dir"]
    csv_dir = REPO_ROOT / config["preprocessing"]["csv_dir"]
    spreadsheet_id = sheets_config["spreadsheet_id"]
    credentials_path = REPO_ROOT / sheets_config["credentials_path"]

    service = build_sheets_service(credentials_path)
    clean_dir(csv_dir)

    for table in load_yaml_dir(tables_dir):
        sheet_name = table["input_csv"]
        rows = download_sheet(service, spreadsheet_id, sheet_name)
        dest = csv_dir / f"{sheet_name}.csv"
        write_csv(rows, dest)
        print(f"downloaded: {sheet_name} -> {dest}")


if __name__ == "__main__":
    try:
        main()
    except (FileNotFoundError, ValueError) as e:
        print(f"エラー: {e}", file=sys.stderr)
        sys.exit(1)
