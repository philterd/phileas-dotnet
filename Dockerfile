# Copyright 2026 Philterd, LLC.
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#     http://www.apache.org/licenses/LICENSE-2.0
#
# Multi-stage build for the Phileas.Rest service ("lite Philter"). Build context is the repository root:
#     docker build -t phileas-rest -f Dockerfile .

# ---- Stage 1: fetch the local GLiNER model (ph-eye-pii-en-small) via git-lfs ----
FROM debian:bookworm-slim AS model
RUN apt-get update \
    && apt-get install -y --no-install-recommends git git-lfs ca-certificates \
    && rm -rf /var/lib/apt/lists/*
RUN git lfs install \
    && git clone --depth 1 https://huggingface.co/philterd/ph-eye-pii-en-small /models/ph-eye-pii-en-small \
    && rm -rf /models/ph-eye-pii-en-small/.git

# ---- Stage 2: build & publish ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source
# Copy the sources needed to restore and publish the service (it project-references the Phileas library).
# README.md and NOTICE are referenced by Phileas.csproj (packaged content), so include them too.
COPY global.json README.md NOTICE ./
COPY src/ ./src/
RUN dotnet publish src/Phileas.Rest/Phileas.Rest.csproj -c Release -o /app

# ---- Stage 3: runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
# Native dependencies:
#   libgomp1                         - ONNX Runtime (GLiNER inference)
#   libfontconfig1 / libfreetype6    - SkiaSharp (PDF rasterization / drawing)
#   tesseract-ocr + tesseract-ocr-eng - OCR engine and English tessdata (scanned PDFs)
#   libtesseract-dev / libleptonica-dev - unversioned .so symlinks the Tesseract .NET wrapper loads
RUN apt-get update \
    && apt-get install -y --no-install-recommends \
        libgomp1 libfontconfig1 libfreetype6 \
        tesseract-ocr tesseract-ocr-eng libtesseract-dev libleptonica-dev \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app ./
COPY --from=model /models /models

ENV ASPNETCORE_URLS=http://+:8080 \
    Phileas__PhEyeModelPath=/models/ph-eye-pii-en-small \
    Phileas__MongoConnectionString=mongodb://mongo:27017 \
    Phileas__ValkeyConnectionString=valkey:6379 \
    # OCR scanned/image pages of PDFs (text-native pages still use the text layer).
    Phileas__Ocr__Mode=Fallback \
    Phileas__Ocr__Language=eng \
    Phileas__Ocr__TessDataPath=/usr/share/tesseract-ocr/5/tessdata

EXPOSE 8080
ENTRYPOINT ["dotnet", "Phileas.Rest.dll"]
