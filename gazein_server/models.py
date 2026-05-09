from pydantic import BaseModel

class GazeSample(BaseModel):
    timestamp:            float
    left_gaze_direction:  list[float]
    right_gaze_direction: list[float]
    left_openness:        float
    right_openness:       float
    face_blend_shapes:    list[float]
    browser_pixel_x:      float
    browser_pixel_y:      float
    hit_canvas:           bool

class GazeChunk(BaseModel):
    chunkId:     str
    userId:      str
    sessionId:   str
    startTime:   float
    endTime:     float
    triggerType: str
    samples:     list[GazeSample]