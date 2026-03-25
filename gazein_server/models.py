from pydantic import BaseModel

class GazeSample(BaseModel):
    timestamp:            float
    left_gaze_x:          float
    left_gaze_y:          float
    left_gaze_z:          float
    right_gaze_x:         float
    right_gaze_y:         float
    right_gaze_z:         float
    left_openness:        float
    right_openness:       float
    face_blend_shapes:    list[float]  # 63개

class GazeChunk(BaseModel):
    chunkId:     str
    startTime:   float
    endTime:     float
    triggerType: str
    samples:     list[GazeSample]