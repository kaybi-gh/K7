#!/usr/bin/env python3
"""Fix missing French diacritics in default (fr) .resx value strings only."""

from __future__ import annotations

import re
import sys
from pathlib import Path

REPLACEMENTS: list[tuple[str, str]] = [
    ("Etes-vous sur de vouloir", "\u00cates-vous s\u00fbr de vouloir"),
    ("Details de lecture", "D\u00e9tails de lecture"),
    ("Details de la session", "D\u00e9tails de la session"),
    ("Details du flux", "D\u00e9tails du flux"),
    ("Details audio", "D\u00e9tails audio"),
    ("Chargement des details...", "Chargement des d\u00e9tails..."),
    ("les details", "les d\u00e9tails"),
    ("Echec de", "\u00c9chec de"),
    ("Echec du", "\u00c9chec du"),
    ("Methodes de connexion", "M\u00e9thodes de connexion"),
    ("Generation de vignettes video", "G\u00e9n\u00e9ration de vignettes vid\u00e9o"),
    ("File enregistree en playlist", "File enregistr\u00e9e en playlist"),
    ("Preferences enregistrees", "Pr\u00e9f\u00e9rences enregistr\u00e9es"),
    ("Preferences serveur enregistrees", "Pr\u00e9f\u00e9rences serveur enregistr\u00e9es"),
    ("Proprietes du morceau", "Propri\u00e9t\u00e9s du morceau"),
    ("Frequence d'echantillonnage", "Fr\u00e9quence d'\u00e9chantillonnage"),
    ("Tonalite", "Tonalit\u00e9"),
    ("Energie", "\u00c9nergie"),
    ("Dansabilite", "Dansabilit\u00e9"),
    ("Ecoutes totales", "\u00c9coutes totales"),
    ("A suivre", "\u00c0 suivre"),
    ("Qualite de streaming", "Qualit\u00e9 de streaming"),
    ("Qualite Wi-Fi", "Qualit\u00e9 Wi-Fi"),
    ("Qualite mobile", "Qualit\u00e9 mobile"),
    (">Qualite<", ">Qualit\u00e9<"),
    (">Precedent<", ">Pr\u00e9c\u00e9dent<"),
    (">Debut<", ">D\u00e9but<"),
    (">Debit<", ">D\u00e9bit<"),
    (">Videos<", ">Vid\u00e9os<"),
    ("Aucune notification configuree", "Aucune notification configur\u00e9e"),
    ("Creez une regle", "Cr\u00e9ez une r\u00egrule"),
    ("lors d'evenements", "lors d'\u00e9v\u00e9nements"),
    ("Quels evenements", "Quels \u00e9v\u00e9nements"),
    ("Selectionnez un ou plusieurs evenements", "S\u00e9lectionnez un ou plusieurs \u00e9v\u00e9nements"),
    ("evenement(s)", "\u00e9v\u00e9nement(s)"),
    ("Parametres de l'evenement", "Param\u00e8tres de l'\u00e9v\u00e9nement"),
    ("Apercu avec des donnees", "Aper\u00e7u avec des donn\u00e9es"),
    (">Apercu<", ">Aper\u00e7u<"),
    ("Cette action est irreversible", "Cette action est irr\u00e9versible"),
]

VALUE_PATTERN = re.compile(r"(<value[^>]*>)(.*?)(</value>)", re.DOTALL)


def fix_value(value: str) -> str:
    for old, new in REPLACEMENTS:
        value = value.replace(old, new)
    return value


def main() -> int:
    root = Path(__file__).resolve().parent.parent / "src" / "Clients" / "Shared" / "UI" / "Resources"
    changed_files = 0
    changed_values = 0

    for path in sorted(root.rglob("*.resx")):
        if path.name.endswith(".en.resx"):
            continue

        original = path.read_text(encoding="utf-8")
        file_changes = 0

        def replacer(match: re.Match[str]) -> str:
            nonlocal file_changes
            prefix, value, suffix = match.group(1), match.group(2), match.group(3)
            fixed = fix_value(value)
            if fixed != value:
                file_changes += 1
            return f"{prefix}{fixed}{suffix}"

        updated = VALUE_PATTERN.sub(replacer, original)
        if updated != original:
            path.write_text(updated, encoding="utf-8", newline="\n")
            changed_files += 1
            changed_values += file_changes
            print(f"{path.relative_to(root)} ({file_changes} values)")

    print()
    print(f"Changed {changed_files} files, {changed_values} value strings.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
