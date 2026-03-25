from fastapi import FastAPI, Request
from fastapi.responses import JSONResponse
from fastapi.exceptions import RequestValidationError
from models import GazeChunk
import pandas as pd
import os

app = FastAPI()

CSV_PATH = "gaze_data.csv"

@app.exception_handler(RequestValidationError)
async def validation_exception_handler(request: Request, exc: RequestValidationError):
    body = await request.body()
    print(f"[422 에러 상세] {exc.errors()}")
    print(f"[받은 JSON 앞부분] {body[:300].decode('utf-8', errors='ignore')}")
    return JSONResponse(status_code=422, content={"detail": exc.errors()})

def chunk_to_rows(chunk: GazeChunk) -> list[dict]:
    rows = []
    for s in chunk.samples:
        row = {
            "chunkId":        chunk.chunkId,
            "triggerType":    chunk.triggerType,
            "timestamp":      s.timestamp,
            "left_gaze_x":    s.left_gaze_direction[0],
            "left_gaze_y":    s.left_gaze_direction[1],
            "left_gaze_z":    s.left_gaze_direction[2],
            "right_gaze_x":   s.right_gaze_direction[0],
            "right_gaze_y":   s.right_gaze_direction[1],
            "right_gaze_z":   s.right_gaze_direction[2],
            "left_openness":  s.left_openness,
            "right_openness": s.right_openness,
        }
        for i, v in enumerate(s.face_blend_shapes):
            row[f"bs_{i}"] = v
        rows.append(row)
    return rows

@app.post("/ingest")
async def ingest(chunk: GazeChunk):
    rows = chunk_to_rows(chunk)
    df   = pd.DataFrame(rows)
    if os.path.exists(CSV_PATH):
        df.to_csv(CSV_PATH, mode="a", header=False, index=False)
    else:
        df.to_csv(CSV_PATH, mode="w", header=True, index=False)
    print(f"[수신] chunkId={chunk.chunkId[:8]}... samples={len(chunk.samples)} → CSV 저장")
    return {"status": "ok", "received": len(chunk.samples)}

@app.get("/health")
async def health():
    return {"status": "ok"}

@app.get("/count")
async def count():
    if not os.path.exists(CSV_PATH):
        return {"rows": 0}
    df = pd.read_csv(CSV_PATH)
    return {"rows": len(df), "chunks": df["chunkId"].nunique()}