## Context

`jsonfmt` は単一プロセスのCLIツールであり、外部サービスや永続化層を持たない。整形ロジック
(`JsonFormatter.cs`) とCLIシェル (`Program.cs`) はすでに分離されている。JSONのパースには
.NET標準の `System.Text.Json` を使用し、新たな外部依存は追加しない。

## Goals / Non-Goals

**Goals:**
- 元のスカラー値（数値の表記、文字列のエスケープなど）を変更せずに整形する
- 単一の入力（引数/ファイル/stdin）と単一の出力（stdout/ファイル）という単純なI/Oモデルを保つ

**Non-Goals:**
- JSON5やコメント付きJSONなど、標準外のJSON方言への対応
- 複数ファイルの一括処理やウォッチモード
- カラー出力や構文ハイライト

## Decisions

- **`JsonDocument` + `GetRawText()` でスカラー値を再出力する（`JsonSerializer` での再シリアライズは行わない）**
  数値の精度や文字列のエスケープ表現を型変換なしに保持できるため。`JsonSerializer.Serialize<object>` で
  再シリアライズすると、数値の指数表記やエスケープが標準化されて元の表記と変わる可能性がある。

- **`JsonSerializerOptions.WriteIndented` を使わず、独自にインデント文字列を組み立てる**
  組み込みの `WriteIndented` はインデント幅のカスタマイズや、要素ごとの改行位置を細かく制御できないため、
  `WriteContainer`/`WriteElement` で再帰的に構築する。

- **位置引数は「既存ファイルパスかどうか」で入力ソースを判定する**
  既存ファイルに一致すればファイル読み込み、一致しなければJSON文字列リテラルとして扱う。追加のフラグ
  （例: `--literal`）を導入せず、利用者が直感的に使える最小のインターフェースを優先する。

- **エラーは例外の型で分類し、CLI層で終了コードへマッピングする**
  JSON解析エラーは `JsonFormatException`、I/Oエラーは `IOException`/`UnauthorizedAccessException` として
  区別し、いずれも `Program.cs` でメッセージを標準エラー出力へ書き、終了コード1を返す。

## Risks / Trade-offs

- [Risk] 位置引数に指定したJSON文字列リテラルが、たまたまローカルの既存ファイルパスと一致すると、
  意図せずファイル内容が読み込まれる → Mitigation: `-i`/`--input` を明示すれば確実にファイル入力になる。
  ヘルプテキストで位置引数の判定規則を明記する。
- [Risk] 巨大なJSON入力に対して `JsonDocument` は全体をメモリに読み込むため、非常に大きな入力では
  メモリ使用量が増える → Mitigation: 現時点ではCLIツールの想定用途（人が読むための整形）では許容範囲と
  判断し、ストリーミング対応は将来の拡張として見送る。
