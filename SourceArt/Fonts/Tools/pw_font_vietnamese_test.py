"""Test fonts and proofs for the Vietnamese module (pw_font_vietnamese), with the Azerbaijani schwa and the Croatian
digraphs. Shares its build and drawing helpers with pw_font_cyrillic_greek_test.

    python pw_font_vietnamese_test.py           # test fonts (Build/Test) and proofs (Proofs/VIET_*.png)
    python pw_font_vietnamese_test.py --fonts   # test fonts only
    python pw_font_vietnamese_test.py --check   # builds twice in memory and compares the bytes
"""
import hashlib
import sys

import pw_font_build as B
import pw_font_cyrillic_greek_test as T

TAG = "Viet"

VIETNAMESE = ["Bạn mới!", "Nhận phần thưởng", "Chạm vào quả trứng để nó nở.",
              "Bạn nhận được 500 viên ngọc và một huy hiệu hiếm.", "Chú nhím ngủ gần những bông hoa.",
              "Thỏ con ăn cỏ, sóc nhỏ leo cây, vịt bơi trên hồ."]
TONES = ["a à á ả ã ạ · ă ằ ắ ẳ ẵ ặ · â ầ ấ ẩ ẫ ậ", "e è é ẻ ẽ ẹ · ê ề ế ể ễ ệ · i ì í ỉ ĩ ị · y ỳ ý ỷ ỹ ỵ",
         "o ò ó ỏ õ ọ · ô ồ ố ổ ỗ ộ · ơ ờ ớ ở ỡ ợ · u ù ú ủ ũ ụ · ư ừ ứ ử ữ ự · đ"]
CAPS = ["PHẦN THƯỞNG HẰNG NGÀY · ĐỘNG VẬT MỚI", "ẮẰẲẴẶ ẤẦẨẪẬ ẾỀỂỄỆ ỐỒỔỖỘ ỚỜỞỠỢ ỨỪỬỮỰ"]
OTHERS = ["Azərbaycan: Yeni dost! Mükafatı topla · ƏLİ, Əli", "Hrvatski: Ǆ ǅ ǆ Ǉ ǈ ǉ Ǌ ǋ ǌ · Džep, Ljubičica, Njuška",
          "Română: Ștrengar · Türkçe: Işık, İğne · Polski: Źdźbło · Čeština: Řeka · Magyar: Őz, Űr"]
LATIN_LINE = "Nouvelle espèce ! Récolter · Hatch 3 eggs"


def main(args):
    import pw_font_vietnamese as V
    if "--check" in args:
        ok = True
        for _, st in T.STYLES:
            a = T.build_test(st, [V], TAG, write=False)[3]
            b = T.build_test(st, [V], TAG, write=False)[3]
            ok &= a == b
            print(f"{T.label(st)}: {'identical' if a == b else 'DIFFERENT'} ({len(a)} bytes)")
        return 0 if ok else 1
    for _, st in T.STYLES:
        path, n, k, blob = T.build_test(st, [V], TAG)
        print(f"{path}: {n} glyphs, {k} kerning pairs, {len(blob)} bytes, sha256 {hashlib.sha256(blob).hexdigest()[:16]}")
    if "--fonts" in args:
        return 0
    out = []
    chars = V.charset()
    for _, st in T.STYLES:
        out.append(T.save(T.grid_sheet(st, TAG, chars, "Vietnamien et latin complété", cols=15), f"VIET_grid_{B.file_name(st)}.png"))
    blocks = [("Tiếng Việt", VIETNAMESE), ("Les tons", TONES), ("Capitales", CAPS), ("Autres langues latines", OTHERS)]
    out.append(T.save(T.sentences_sheet(TAG, blocks, False), "VIET_sentences_light.png"))
    out.append(T.save(T.sentences_sheet(TAG, blocks, True), "VIET_sentences_dark.png"))
    plush = B.FAMILIES["PocketPlush"][0]
    out.append(T.save(T.sentences_sheet(TAG, [("Pocket Plush", VIETNAMESE[:3] + CAPS)], False, (96, 48, 28), plush),
                      "VIET_titles_plush.png"))
    out.append(T.save(T.styles_sheet(TAG, LATIN_LINE, ["Bạn mới! Nhận phần thưởng", "PHẦN THƯỞNG HẰNG NGÀY"]), "VIET_styles.png"))
    out.append(T.save(T.metrics_sheet(TAG, "ẮẲẴẤẨẪỖỚỰỴấẳẫởựỵ", "Marques empilées dans la hauteur de ligne (ascendante 950, win 1000)"),
                      "VIET_marks_metrics.png"))
    for p in out:
        print(p)
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
