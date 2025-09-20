# Use an NVIDIA CUDA base image with Python 3
FROM nvidia/cuda:12.8.1-devel-ubuntu22.04

# Install Python 3 and pip
RUN apt-get update && \
    apt-get install -y python3 python3-pip git && \
    apt-get clean

# Set working directory
WORKDIR /app

# Copy requirements first for better caching
COPY /ParlerTTSAPI/requirements.txt .

# Install Python dependencies
RUN pip3 install --no-cache-dir -r requirements.txt

# Install Parler TTS
RUN pip3 install git+https://github.com/huggingface/parler-tts.git

# Copy application code
COPY /ParlerTTSAPI/source .

# Expose port
EXPOSE 8000

# Set environment variables
ENV HF_HOME=/cache/huggingface

# Run the application
CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8000"]
