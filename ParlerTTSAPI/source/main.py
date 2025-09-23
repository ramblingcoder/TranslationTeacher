from fastapi import FastAPI, File, UploadFile, Form
from pydantic import BaseModel
from pathlib import Path
import torch
from parler_tts import ParlerTTSForConditionalGeneration
from transformers import AutoTokenizer
import soundfile as sf
import io
from fastapi.responses import Response
from fastapi.middleware.cors import CORSMiddleware

app = FastAPI(title="Parler TTS API")

# Add CORS middleware
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],  # Allows all origins
    allow_credentials=True,
    allow_methods=["*"],  # Allows all methods
    allow_headers=["*"],  # Allows all headers
)

class TextToSpeechRequest(BaseModel):
    text: str
    speaker: str = "A female speaker delivers a slightly expressive and animated speech with a moderate speed and pitch. The recording is of very high quality, with the speaker's voice sounding clear and very close up."

# Initialize pipelines (this will be loaded once)
ttsModel = None
tokenizer = None
description_tokenizer = None
device = None

@app.on_event("startup")
async def load_pipeline():
    global ttsModel, tokenizer, description_tokenizer, device
    
    device = "cuda:0" if torch.cuda.is_available() else "cpu"
    
    # Load the pre-trained diarization pipeline
    ttsModel = ParlerTTSForConditionalGeneration.from_pretrained("parler-tts/parler-tts-mini-multilingual-v1.1").to(device)
    tokenizer = AutoTokenizer.from_pretrained("parler-tts/parler-tts-mini-multilingual-v1.1")
    description_tokenizer = AutoTokenizer.from_pretrained(ttsModel.config.text_encoder._name_or_path)

@app.post("/tts")
async def text_to_speech(request: TextToSpeechRequest):
    """
    Convert text to speech using Parler TTS
    """
    try:
        input_ids = description_tokenizer(request.speaker, return_tensors="pt").input_ids.to(device)
        prompt_input_ids = tokenizer(request.text, return_tensors="pt").input_ids.to(device)
        
        generation = ttsModel.generate(input_ids=input_ids, prompt_input_ids=prompt_input_ids)
        audio_arr = generation.cpu().numpy().squeeze()
        
        # Create in-memory WAV file
        buffer = io.BytesIO()
        sf.write(buffer, audio_arr, samplerate=ttsModel.config.sampling_rate, format='WAV')
        buffer.seek(0)
        audio_bytes = buffer.read()
        
        # Return raw bytes with proper content type
        return Response(content=audio_bytes, media_type="audio/wav")
    
    except Exception as e:
        return {"error": str(e)}

@app.get("/health")
async def health_check():
    """Health check endpoint"""
    return {"status": "healthy"}

if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8000)