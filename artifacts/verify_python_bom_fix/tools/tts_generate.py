import argparse
from pathlib import Path

from gtts import gTTS


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Generate speech audio from text.")
    parser.add_argument("--text", required=True, help="Text to synthesize.")
    parser.add_argument("--output", required=True, help="Path to output mp3 file.")
    parser.add_argument("--voice", default="vi-VN-HoaiMyNeural", help="Voice name or language hint.")
    parser.add_argument("--rate", default="+0%", help="Reserved for compatibility with the .NET caller.")
    return parser.parse_args()


def resolve_lang(voice_name: str) -> str:
    normalized = (voice_name or "").strip().lower()
    if normalized.startswith("vi"):
        return "vi"
    if normalized.startswith("en"):
        return "en"
    if normalized.startswith("zh") or normalized.startswith("cmn"):
        return "zh-CN"
    if normalized.startswith("ja"):
        return "ja"
    if normalized.startswith("ko"):
        return "ko"
    return "vi"


def main() -> None:
    args = parse_args()
    output_path = Path(args.output).resolve()
    output_path.parent.mkdir(parents=True, exist_ok=True)
    speech = gTTS(text=args.text, lang=resolve_lang(args.voice), slow=False)
    speech.save(str(output_path))


if __name__ == "__main__":
    main()
