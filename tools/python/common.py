"""前処理・配置スクリプト共通のユーティリティ。"""

from __future__ import annotations

import shutil
import sys
from pathlib import Path
from typing import Any

import yaml

# Windows(cp932コンソール)でも日本語の標準出力が文字化けしないようにする
for _stream in (sys.stdout, sys.stderr):
    try:
        _stream.reconfigure(encoding="utf-8")
    except (AttributeError, ValueError):
        pass

# 生成・コピーされた全ファイルの先頭に付与するコメント。
# 各スクリプトは、出力する言語のコメント構文に合わせて改行・記号を調整して使う。
AUTO_GENERATED_COMMENT = (
    "DO NOT EDIT: このファイルは master-data-pipeline によって自動生成されています。"
    "直接編集せず、スプレッドシートまたは schema/ 配下の定義を変更してください。"
)

REPO_ROOT = Path(__file__).resolve().parents[2]


def load_config(config_path: Path | str = REPO_ROOT / "config.yaml") -> dict[str, Any]:
    """ルートの config.yaml を読み込む。"""
    with open(config_path, encoding="utf-8") as f:
        return yaml.safe_load(f)


def clean_dir(path: Path | str) -> Path:
    """生成・コピー直前に呼び出す共通クリーンアップ。

    ディレクトリが存在すれば削除してから空の状態で再作成する。
    これにより、テーブル削除・リネーム時に古い生成物が残らないことを保証する。
    """
    path = Path(path)
    if path.exists():
        shutil.rmtree(path)
    path.mkdir(parents=True, exist_ok=True)
    return path


def clean_dirs(paths: list[Path | str]) -> None:
    """複数ディレクトリをまとめてクリーンアップする。"""
    for path in paths:
        clean_dir(path)


def load_yaml_dir(path: Path | str) -> list[dict[str, Any]]:
    """ディレクトリ直下の *.yaml をファイル名順にすべて読み込む。

    schema/tables/*.yaml, schema/enums/*.yaml の読み込みに使う共通処理。
    """
    files = sorted(Path(path).glob("*.yaml"))
    result = []
    for file in files:
        with open(file, encoding="utf-8") as f:
            result.append(yaml.safe_load(f))
    return result


def copy_dir_contents(src_dir: Path | str, dest_dir: Path | str, pattern: str = "*") -> list[Path]:
    """dest_dirをクリーンアップしてから、src_dir内でpatternに一致するファイルをコピーする。

    dest_dirがそのスクリプトの生成物専用ディレクトリである場合に使う
    (他の生成物・手書きファイルと同居するディレクトリには copy_file を使うこと)。
    """
    src_dir = Path(src_dir)
    dest_dir = clean_dir(dest_dir)
    copied = []
    for src in sorted(src_dir.glob(pattern)):
        dest = dest_dir / src.name
        shutil.copy2(src, dest)
        copied.append(dest)
    return copied


def copy_file(src: Path | str, dest_dir: Path | str) -> Path:
    """単一ファイルをdest_dirにコピーする。dest_dirが無ければ作成するが、
    既存の他ファイルは削除しない(Models/Enums/Cryptographyなどと同居する
    ディレクトリへのコピーに使う)。
    """
    src = Path(src)
    dest_dir = Path(dest_dir)
    dest_dir.mkdir(parents=True, exist_ok=True)
    dest = dest_dir / src.name
    shutil.copy2(src, dest)
    return dest
