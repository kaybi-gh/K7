#!/usr/bin/env bash
set -euo pipefail

MEDIA_ROOT="${MEDIA_ROOT:-/k7/media}"

die() {
    echo "ERROR: $*" >&2
    exit 1
}

require_cmd() {
    command -v "$1" >/dev/null 2>&1 || die "Missing required command: $1"
}

ensure_dir() {
    mkdir -p "$1"
}

# Strip characters that break path handling or FFMpegCore argument quoting.
sanitize_path_segment() {
    local value="$1"
    value="${value//\\// - }"
    value="${value//\// - }"
    value="${value//\"/}"
    value="${value//:/ - }"
    value="${value//$'\n'/ }"
    value="${value//$'\r'/ }"
    value="${value//  / }"
    while [[ "$value" == *'  '* ]]; do
        value="${value//  / }"
    done
    value="${value%"${value##*[![:space:]]}"}"
    value="${value#"${value%%[![:space:]]*}"}"
    while [[ "$value" == *. ]]; do
        value="${value%.}"
    done
    printf '%s' "$value"
}

download_file() {
    local url="$1"
    local out="$2"

    if [[ -f "$out" ]]; then
        echo "SKIP exists: $out"
        return 0
    fi

    ensure_dir "$(dirname "$out")"
    echo "DOWNLOAD: $out"
    curl -fL --retry 3 --retry-delay 5 -o "$out" "$url"
}

uri_encode() {
    jq -rn --arg v "$1" '$v|@uri'
}

archive_download_url() {
    local id="$1"
    local filename="$2"
    local encoded=""
    local part

    filename="${filename//\\//}"
    IFS='/' read -ra parts <<< "$filename"
    for part in "${parts[@]}"; do
        if [[ -n "$encoded" ]]; then
            encoded+="/"
        fi
        encoded+=$(uri_encode "$part")
    done

    echo "https://archive.org/download/${id}/${encoded}"
}

get_archive_video_file() {
    local id="$1"
    local meta

    meta=$(curl -fsS "https://archive.org/metadata/${id}")

    if echo "$meta" | jq -e '.error' >/dev/null 2>&1; then
        die "Archive metadata error for ${id}: $(echo "$meta" | jq -r '.error')"
    fi

    echo "$meta" | jq -r '
        def archive_files:
            if (.files | type) == "array" then
                [.files[] | select(type == "object" and (.name | type) == "string")]
            else
                []
            end;
        def video_candidates($allow_derivative):
            archive_files
            | map(select(.name | test("\\.(mp4|mkv|mpeg|avi)$"; "i")))
            | map(select(.name | test("thumb|gif|512kb|_reviews"; "i") | not))
            | if $allow_derivative then . else map(select((.source // "") != "derivative")) end;
        (video_candidates(false) + video_candidates(true))
        | unique_by(.name)
        | sort_by((.size // "0") | if type == "string" then tonumber else . end)
        | reverse
        | .[0].name // empty'
}

download_archive_video() {
    local id="$1"
    local out="$2"
    local filename url

    if [[ -f "$out" ]]; then
        echo "SKIP exists: $out"
        return 0
    fi

    filename=$(get_archive_video_file "$id")
    [[ -n "$filename" ]] || die "No video file for archive item: $id"

    url=$(archive_download_url "$id" "$filename")
    echo "DOWNLOAD: $out"
    echo "  <- ${id} / ${filename}"
    download_file "$url" "$out"
}

download_archive_cover() {
    local archive_id="$1"
    local out_dir="$2"
    local out="${out_dir}/cover.jpg"
    local url

    if [[ -f "$out" ]]; then
        echo "SKIP exists: $out"
        return 0
    fi

    url=$(archive_download_url "$archive_id" "cover.jpg")
    echo "COVER: $out"
    download_file "$url" "$out"
}

download_musopen_album() {
    local artist="$1"
    local album="$2"
    local archive_id="$3"
    local cover_url="${4:-}"
    shift 4

    local -a track_titles=("$@")
    local safe_artist safe_album
    local out_dir meta
    local -a archive_files=()
    local index=0
    local archive_file title safe_title out url

    safe_artist=$(sanitize_path_segment "$artist")
    safe_album=$(sanitize_path_segment "$album")
    out_dir="${MEDIA_ROOT}/music/${safe_artist}/${safe_album}"

    meta=$(curl -fsS "https://archive.org/metadata/${archive_id}")

    if echo "$meta" | jq -e '.error' >/dev/null 2>&1; then
        die "Archive metadata error for ${archive_id}: $(echo "$meta" | jq -r '.error')"
    fi

    mapfile -t archive_files < <(echo "$meta" | jq -r '
        [if (.files | type) == "array" then .files else [] end
            | .[]
            | select(type == "object" and (.name | type) == "string")
            | select(.name | endswith(".mp3"))
            | .name]
        | sort[]')

    [[ ${#archive_files[@]} -gt 0 ]] || die "No mp3 files for archive item: ${archive_id}"

    if [[ ${#track_titles[@]} -ne ${#archive_files[@]} ]]; then
        die "Track title count (${#track_titles[@]}) does not match mp3 count (${#archive_files[@]}) for ${archive_id}"
    fi

    echo "ALBUM: ${safe_artist} - ${safe_album}"
    echo "  Musopen release (${archive_id})"

    for archive_file in "${archive_files[@]}"; do
        index=$((index + 1))
        title="${track_titles[$((index - 1))]}"
        safe_title=$(sanitize_path_segment "$title")
        out=$(printf '%s/%02d - %s.mp3' "$out_dir" "$index" "$safe_title")
        url=$(archive_download_url "$archive_id" "$archive_file")
        echo "  <- ${archive_file}"
        download_file "$url" "$out"
    done

    if [[ -n "$cover_url" ]]; then
        download_file "$cover_url" "${out_dir}/cover.jpg"
    fi
}

download_open_goldberg_album() {
    local artist='Johann Sebastian Bach'
    local album='Goldberg Variations, BWV 988'
    local archive_id='OpenGoldbergVariations'
    local safe_artist safe_album
    local out_dir="${MEDIA_ROOT}/music/$(sanitize_path_segment "$artist")/$(sanitize_path_segment "$album")"

    local -a archive_files=(
        'Kimiko Ishizaka - J.S. Bach- -Open- Goldberg Variations, BWV 988 (Piano) - 01 Aria.mp3'
        'Kimiko Ishizaka - J.S. Bach- -Open- Goldberg Variations, BWV 988 (Piano) - 02 Variatio 1 a 1 Clav..mp3'
        'Kimiko Ishizaka - J.S. Bach- -Open- Goldberg Variations, BWV 988 (Piano) - 03 Variatio 2 a 1 Clav..mp3'
        'Kimiko Ishizaka - J.S. Bach- -Open- Goldberg Variations, BWV 988 (Piano) - 04 Variatio 3 a 1 Clav. Canone all Unisuono.mp3'
        'Kimiko Ishizaka - J.S. Bach- -Open- Goldberg Variations, BWV 988 (Piano) - 05 Variatio 4 a 1 Clav..mp3'
        'Kimiko Ishizaka - J.S. Bach- -Open- Goldberg Variations, BWV 988 (Piano) - 06 Variatio 5 a 1 ovvero 2 Clav..mp3'
        'Kimiko Ishizaka - J.S. Bach- -Open- Goldberg Variations, BWV 988 (Piano) - 07 Variatio 6 a 1 Clav. Canone alla Seconda.mp3'
        'Kimiko Ishizaka - J.S. Bach- -Open- Goldberg Variations, BWV 988 (Piano) - 08 Variatio 7 a 1 ovvero 2 Clav..mp3'
        'Kimiko Ishizaka - J.S. Bach- -Open- Goldberg Variations, BWV 988 (Piano) - 09 Variatio 8 a 2 Clav..mp3'
        'Kimiko Ishizaka - J.S. Bach- -Open- Goldberg Variations, BWV 988 (Piano) - 10 Variatio 9 a 1 Clav. Canone alla Terza.mp3'
        'Kimiko Ishizaka - J.S. Bach- -Open- Goldberg Variations, BWV 988 (Piano) - 11 Variatio 10 a 1 Clav. Fughetta.mp3'
        'Kimiko Ishizaka - J.S. Bach- -Open- Goldberg Variations, BWV 988 (Piano) - 12 Variatio 11 a 2 Clav..mp3'
        'Kimiko Ishizaka - J.S. Bach- -Open- Goldberg Variations, BWV 988 (Piano) - 13 Variatio 12 Canone alla Quarta.mp3'
        'Kimiko Ishizaka - J.S. Bach- -Open- Goldberg Variations, BWV 988 (Piano) - 14 Variatio 13 a 2 Clav..mp3'
        'Kimiko Ishizaka - J.S. Bach- -Open- Goldberg Variations, BWV 988 (Piano) - 15 Variatio 14 a 2 Clav..mp3'
        'KimikoIshizaka-J.s.Bach--open-GoldbergVariationsBwv988piano-16Variatio15A1Clav.CanoneAllaQuinta.mp3'
        'KimikoIshizaka-J.s.Bach--open-GoldbergVariationsBwv988piano-17Variatio16A1Clav.Ouverture.mp3'
        'KimikoIshizaka-J.s.Bach--open-GoldbergVariationsBwv988piano-18Variatio17A2Clav..mp3'
        'KimikoIshizaka-J.s.Bach--open-GoldbergVariationsBwv988piano-19Variatio18A1Clav.CanoneAllaSexta.mp3'
        'KimikoIshizaka-J.s.Bach--open-GoldbergVariationsBwv988piano-20Variatio19A1Clav..mp3'
        'KimikoIshizaka-J.s.Bach--open-GoldbergVariationsBwv988piano-21Variatio20A2Clav..mp3'
        'KimikoIshizaka-J.s.Bach--open-GoldbergVariationsBwv988piano-22Variatio21CanoneAllaSettima.mp3'
        'KimikoIshizaka-J.s.Bach--open-GoldbergVariationsBwv988piano-23Variatio22A1Clav..mp3'
        'Kimiko Ishizaka - J.S. Bach- -Open- Goldberg Variations, BWV 988 (Piano) - 24 Variatio 23 a 2 Clav..mp3'
        'KimikoIshizaka-J.s.Bach--open-GoldbergVariationsBwv988piano-25Variatio24A1Clav.CanoneAllOttava.mp3'
        'KimikoIshizaka-J.s.Bach--open-GoldbergVariationsBwv988piano-26Variatio25A2Clav..mp3'
        'KimikoIshizaka-J.s.Bach--open-GoldbergVariationsBwv988piano-27Variatio26A2Clav..mp3'
        'KimikoIshizaka-J.s.Bach--open-GoldbergVariationsBwv988piano-28Variatio27A2Clav.CanoneAllaNona-Variatio28A2Clav..mp3'
        'KimikoIshizaka-J.s.Bach--open-GoldbergVariationsBwv988piano-29Variatio29A1Ovvero2Clav..mp3'
        'KimikoIshizaka-J.s.Bach--open-GoldbergVariationsBwv988piano-30Variatio30A1Clav.Quodlibet.mp3'
        'KimikoIshizaka-J.s.Bach--open-GoldbergVariationsBwv988piano-31AriaDaCapoEFine.mp3'
    )

    local -a track_titles=(
        'Aria'
        'Variatio 1 a 1 Clav.'
        'Variatio 2 a 1 Clav.'
        'Variatio 3 a 1 Clav. Canone all Unisuono'
        'Variatio 4 a 1 Clav.'
        'Variatio 5 a 1 ovvero 2 Clav.'
        'Variatio 6 a 1 Clav. Canone alla Seconda'
        'Variatio 7 a 1 ovvero 2 Clav.'
        'Variatio 8 a 2 Clav.'
        'Variatio 9 a 1 Clav. Canone alla Terza'
        'Variatio 10 a 1 Clav. Fughetta'
        'Variatio 11 a 2 Clav.'
        'Variatio 12 Canone alla Quarta'
        'Variatio 13 a 2 Clav.'
        'Variatio 14 a 2 Clav.'
        'Variatio 15 a 1 Clav. Canone alla Quinta'
        'Variatio 16 a 1 Clav. Ouverture'
        'Variatio 17 a 2 Clav.'
        'Variatio 18 a 1 Clav. Canone alla Sexta'
        'Variatio 19 a 1 Clav.'
        'Variatio 20 a 2 Clav.'
        'Variatio 21 Canone alla Settima'
        'Variatio 22 a 1 Clav.'
        'Variatio 23 a 2 Clav.'
        'Variatio 24 a 1 Clav. Canone all Ottava'
        'Variatio 25 a 2 Clav.'
        'Variatio 26 a 2 Clav.'
        'Variatio 27 a 2 Clav. Canone alla Nona - Variatio 28 a 2 Clav.'
        'Variatio 29 a 1 ovvero 2 Clav.'
        'Variatio 30 a 1 Clav. Quodlibet'
        'Aria da Capo e Fine'
    )

    local index=0
    local archive_file title safe_title out url

    safe_artist=$(sanitize_path_segment "$artist")
    safe_album=$(sanitize_path_segment "$album")

    echo "ALBUM: ${safe_artist} - ${safe_album}"
    echo "  CC0 Open Goldberg Variations (${archive_id})"

    for archive_file in "${archive_files[@]}"; do
        index=$((index + 1))
        title="${track_titles[$((index - 1))]}"
        safe_title=$(sanitize_path_segment "$title")
        out=$(printf '%s/%02d - %s.mp3' "$out_dir" "$index" "$safe_title")
        url=$(archive_download_url "$archive_id" "$archive_file")
        echo "  <- ${archive_file}"
        download_file "$url" "$out"
    done

    download_archive_cover "$archive_id" "$out_dir"
}

episode_out() {
    local series_root="$1"
    local n="$2"
    local title="$3"
    printf '%s/Superman (1941) - S01E%02d - %s.mp4' "$series_root" "$n" "$title"
}

download_superman_path() {
    local series_root="$1"
    local n="$2"
    local title="$3"
    local id="$4"
    local path="$5"
    local out url

    out=$(episode_out "$series_root" "$n" "$title")
    url="https://archive.org/download/${id}/${path// /%20}"
    download_file "$url" "$out"
}

download_superman_archive() {
    local series_root="$1"
    local n="$2"
    local title="$3"
    local id="$4"
    local out

    out=$(episode_out "$series_root" "$n" "$title")
    download_archive_video "$id" "$out"
}

pioneer_episode_out() {
    local series_root="$1"
    local n="$2"
    local title="$3"
    printf '%s/Pioneer One - S01E%02d - %s.mp4' "$series_root" "$n" "$title"
}

download_pioneer_one_episode() {
    local series_root="$1"
    local n="$2"
    local title="$3"
    local archive_path="$4"
    local out url

    out=$(pioneer_episode_out "$series_root" "$n" "$title")
    url=$(archive_download_url 'pioneer.one.season1.720p.x264-vodo' "$archive_path")
    download_file "$url" "$out"
}

require_cmd curl
require_cmd jq

movies_root="${MEDIA_ROOT}/movies"
superman_root="${MEDIA_ROOT}/series/Superman (1941)/Season 01"
pioneer_root="${MEDIA_ROOT}/series/Pioneer One/Season 01"
music_root="${MEDIA_ROOT}/music"

ensure_dir "$movies_root"
ensure_dir "$(dirname "$superman_root")"
ensure_dir "$(dirname "$pioneer_root")"
ensure_dir "$music_root"

echo "== Movies =="

# H.264 + AAC stereo MP4 for reliable direct play in K7 (browser / HLS).
# Blender .mov (BBB AAC 5.1) and .mkv (Sintel AC3 5.1) often fail without transcoding.

download_file \
    "$(archive_download_url 'BigBuckBunnyOfficialBlenderFoundationShortFilm720p30fps' 'Big Buck Bunny Official Blender Foundation Short Film 720p 30fps.mp4')" \
    "${movies_root}/Big Buck Bunny (2008)/Big Buck Bunny (2008).mp4"

download_file \
    "$(archive_download_url 'sintel_cc_by_2010' 'sintel-2048-stereo.mp4')" \
    "${movies_root}/Sintel (2010)/Sintel (2010).mp4"

download_file \
    "$(archive_download_url 'sprite-fright-2021' 'Sprite Fright (2021).mp4')" \
    "${movies_root}/Sprite Fright (2021)/Sprite Fright (2021).mp4"

echo "== Music (MusicBrainz-friendly, with cover art) =="

download_open_goldberg_album

download_musopen_album \
    'Johannes Brahms' \
    'Symphony No. 1 in C minor, Op. 68' \
    'SymphonyNo.1InCMinorOp.68' \
    'https://upload.wikimedia.org/wikipedia/commons/c/cc/JohannesBrahms_%28cropped%29.jpg' \
    'I. Un poco sostenuto - Allegro' \
    'II. Andante sostenuto' \
    'III. Un poco allegretto e grazioso' \
    'IV. Adagio - Allegro non troppo, ma con brio'

echo "== Series: Superman (1941) =="

download_superman_path "$superman_root" 1 'Superman' 'Superman_CC0' 'superman_CC0/Superman-01-The_Mad_Scientist.mp4'
download_superman_archive "$superman_root" 2 'The Mechanical Monsters' 'superman_mechanical_monsters'
download_superman_path "$superman_root" 3 'Billion Dollar Limited' 'Superman_CC0' 'superman_CC0/Superman-03-Billion_Dollar_Limited.mp4'
download_superman_archive "$superman_root" 4 'The Arctic Giant' 'arctic_giant'
download_superman_archive "$superman_root" 5 'The Bulleteers' 'bulleteers'
download_superman_path "$superman_root" 6 'The Magnetic Telescope' 'Superman_CC0' 'superman_CC0/Superman-06-The_Magnetic_Telescope.mp4'
download_superman_path "$superman_root" 7 'Electric Earthquake' 'Superman_CC0' 'superman_CC0/Superman-07-Electric_Earthquake.mp4'
download_superman_path "$superman_root" 8 'Volcano' 'Superman_CC0' 'superman_CC0/Superman-08-Volcano.mp4'
download_superman_path "$superman_root" 9 'Terror on the Midway' 'Superman_CC0' 'superman_CC0/Superman-09-Terror_on_the_Midway.mp4'
download_superman_path "$superman_root" 10 'Japoteurs' 'Superman_CC0' 'superman_CC0/Superman-10-Japoteurs.mp4'
download_superman_path "$superman_root" 11 'Showdown' 'Superman_CC0' 'superman_CC0/Superman-11-Showdown.mp4'
download_superman_archive "$superman_root" 12 'Eleventh Hour' 'superman_eleventh_hour'
download_superman_archive "$superman_root" 13 'Destruction Inc' 'superman_destruction_inc'
download_superman_archive "$superman_root" 14 'The Mummy Strikes' 'mummy_strikes'
download_superman_archive "$superman_root" 15 'Jungle Drums' 'superman_jungle_drums'
download_superman_path "$superman_root" 16 'The Underground World' 'Superman_CC0' 'superman_CC0/Superman-16-The_Underground_World.mp4'
download_superman_path "$superman_root" 17 'Secret Agent' 'Superman_CC0' 'superman_CC0/Superman-17-Secret_Agent.mp4'

echo "== Series: Pioneer One =="

download_pioneer_one_episode "$pioneer_root" 1 'Earthfall (Pilot)' \
    'Pioneer.One.SEASON1.720p.x264-VODO/Pioneer.One.S01E01.720p.x264-VODO.mp4'
download_pioneer_one_episode "$pioneer_root" 2 'The Man From Mars' \
    'Pioneer.One.SEASON1.720p.x264-VODO/Pioneer.One.S01E02.720p.x264-VODO.mp4'
download_pioneer_one_episode "$pioneer_root" 3 'Alone in the Night' \
    'Pioneer.One.SEASON1.720p.x264-VODO/Pioneer.One.S01E03.720p.x264-VODO.mp4'
download_pioneer_one_episode "$pioneer_root" 4 'Triangular Diplomacy' \
    'Pioneer.One.SEASON1.720p.x264-VODO/Pioneer.One.S01E04.720p.x264-VODO.mp4'
download_pioneer_one_episode "$pioneer_root" 5 'Sea Change' \
    'Pioneer.One.SEASON1.720p.x264-VODO/Pioneer.One.S01E05.720p.x264-VODO.mp4'
download_pioneer_one_episode "$pioneer_root" 6 'War of the World' \
    'Pioneer.One.SEASON1.720p.x264-VODO/Pioneer.One.S01E06.720p.x264-VODO.mp4'

echo 'DONE'
