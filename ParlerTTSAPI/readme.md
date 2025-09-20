```bash
docker compose up --build
```

```bash
# Speaker argument optional.
curl -X POST "http://localhost:8000/tts" \
  -H "Content-Type: application/json" \
  -d '{"text": "¿Puede recomendarme algún restaurante cerca de aquí?", "speaker":"Olivia has a monotone voice speaking in a calm tone and pace, with a very close recording that almost has no background noise."}' \
  --output output.wav
```