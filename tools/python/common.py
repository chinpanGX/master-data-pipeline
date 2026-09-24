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


def resolve_dest_dir(dest: dict[str, Any], key: str) -> Path | None:
    """copy_destinations[key] を解決する。

    値が空文字(未設定)ならNoneを返す。呼び出し側はこれを「まだ配置先が
    存在しないので、このコピーはスキップする」の合図として使うこと
    (空文字を REPO_ROOT に解決してしまうとリポジトリ直下を巻き込んだ
    クリーンアップ・コピーが走ってしまうため、ここで弾く)。
    """
    value = dest[key]
    if not value:
        return None
    return (REPO_ROOT / value).resolve()


def copy_dir_contents(
    src_dir: Path | str,
    dest_dir: Path | str,
    pattern: str = "*",
    preserve_meta: bool = False,
) -> list[Path]:
    """dest_dirをクリーンアップしてから、src_dir内でpatternに一致するファイルをコピーする。

    dest_dirがそのスクリプトの生成物専用ディレクトリである場合に使う
    (他の生成物・手書きファイルと同居するディレクトリには copy_file を使うこと)。

    preserve_meta=True にすると、Unityの `.meta` サイドカーファイルのうち
    対応する実体ファイルが今回も引き続きコピーされるものは削除しない
    (rmtreeで一律削除すると、その実体ファイルのGUIDが次のUnityインポート時に
    再採番され、Addressables等からのGUID参照が切れてしまうため)。
    実体ファイルが無くなった(テーブル削除・リネーム等の)孤児`.meta`は
    通常どおり削除する。dest_dir配下がUnityプロジェクト内にある
    コピー先(masterdata.bytes、Models/Enumsの*.cs)でのみ使うこと。
    """
    src_dir = Path(src_dir)
    dest_dir = Path(dest_dir)

    if preserve_meta:
        src_names = {src.name for src in Path(src_dir).glob(pattern)}
        if dest_dir.exists():
            for existing in dest_dir.iterdir():
                if existing.name.endswith(".meta") and existing.name[: -len(".meta")] in src_names:
                    continue  # 対応する実体ファイルが今回もコピーされるのでGUIDを維持する
                if existing.is_dir():
                    shutil.rmtree(existing)
                else:
                    existing.unlink()
        else:
            dest_dir.mkdir(parents=True, exist_ok=True)
    else:
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
