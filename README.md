# MoodStabilizer: Emotion-Aware Adaptive Bedroom Environment

**MoodStabilizer** is a research-level, simulation-based AI concept for an emotionally adaptive bedroom.  
The system monitors non-intrusive, multimodal behavioural signals and adapts the environment to support **happiness, emotional stability, and sustainable productivity**.

> Note: This repository currently contains the full **technical specification** of the project as a Word document (master’s-thesis style), not an implementation yet.

## Project Overview

MoodStabilizer is designed as a hybrid AI system that:

- Uses **simulated sensor inputs** such as:
  - Motion and footsteps  
  - Door slam intensity  
  - Voice intensity and tone (no raw audio)  
  - Posture (standing/sitting/lying)  
  - Smartphone usage patterns  
  - Time of day and presence of other people  
- Extracts structured features and feeds them to a **neural network (MLP)** to classify:
  - sad, stressed, angry, tired, happy, neutral, productive  
- Wraps the model in a **rule-based decision layer** to:
  - enforce privacy and safety rules  
  - choose interventions (lighting, ambient audio, notifications, silence)  
- Maintains a **LongTermProfileManager** that stores encrypted, local summaries of routines, happiness triggers, and productivity cycles for personalization.

## Document

The main document in this repository describes:

- System objectives and requirements  
- Architecture and data model  
- AI design and training strategy (synthetic data, evaluation metrics)  
- Simulation design and scenarios  
- Security, privacy, and ethical framework  
- Limitations and future work  

It is written at a **master’s dissertation level**, even though the author is a bachelor student.

## Author

**Mohamed Chaibi**  
- Computer Science student (3rd year), Monastir, Tunisia  
- Email: `medchaibi965@proton.me`  
- GitHub: [@medchaibi16](https://github.com/medchaibi16)
