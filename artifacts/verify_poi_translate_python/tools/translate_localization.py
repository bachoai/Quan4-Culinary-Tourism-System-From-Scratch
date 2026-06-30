import argparse
import json
import sys
from pathlib import Path

from deep_translator import GoogleTranslator


LANGUAGE_MAP = {
    "vi": "vi",
    "en": "en",
    "zh": "zh-CN",
    "ja": "ja",
    "ko": "ko",
    "fr": "fr",
    "de": "de",
    "es": "es",
    "th": "th",
    "ru": "ru",
}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Translate POI localization payload.")
    parser.add_argument("--input", required=True, help="Path to input JSON payload.")
    return parser.parse_args()


def resolve_language(code: str) -> str:
    normalized = (code or "").strip().lower()
    return LANGUAGE_MAP.get(normalized, normalized or "vi")


def translate_text(translator: GoogleTranslator, value: str) -> str:
    if not value or not value.strip():
        return ""
    translated = translator.translate(value.strip())
    return translated.strip() if isinstance(translated, str) else ""


def main() -> int:
    args = parse_args()
    payload = json.loads(Path(args.input).read_text(encoding="utf-8"))

    source_lang = resolve_language(payload.get("sourceLang", "vi"))
    target_lang = resolve_language(payload.get("targetLang", "en"))
    translator = GoogleTranslator(source=source_lang, target=target_lang)

    result = {
        "name": translate_text(translator, payload.get("name", "")),
        "description": translate_text(translator, payload.get("description", "")),
        "ttsScript": translate_text(translator, payload.get("ttsScript", "")),
    }

    if not result["name"] or not result["description"]:
        raise ValueError("Translated payload is missing required fields.")

    sys.stdout.write(json.dumps(result, ensure_ascii=False))
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except Exception as exception:  # noqa: BLE001
        print(str(exception), file=sys.stderr)
        raise SystemExit(1)
