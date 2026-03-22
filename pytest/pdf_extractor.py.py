import fitz
import json
import sys

def extract_text_coords(pdf_path: str) -> list[dict]:
    doc    = fitz.open(pdf_path)
    result = []

    for page_num, page in enumerate(doc):
        W, H   = page.rect.width, page.rect.height
        blocks = page.get_text("dict")["blocks"]

        for block in blocks:
            if block["type"] != 0:
                continue
            for line in block["lines"]:
                for span in line["spans"]:
                    bbox = span["bbox"]
                    cx   = (bbox[0] + bbox[2]) / 2
                    cy   = (bbox[1] + bbox[3]) / 2
                    result.append({
                        "page": page_num,
                        "text": span["text"],
                        "u":    round(cx / W, 6),
                        "v":    round(cy / H, 6),
                    })
    doc.close()
    return result

if __name__ == "__main__":
    path   = sys.argv[1] if len(sys.argv) > 1 else "study.pdf"
    coords = extract_text_coords(path)
    with open("text_coords.json", "w", encoding="utf-8") as f:
        json.dump(coords, f, ensure_ascii=False, indent=2)
    print(f"완료: {len(coords)}개 텍스트 블록 → text_coords.json")