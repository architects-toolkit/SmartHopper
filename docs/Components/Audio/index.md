# Audio Components

Audio components provide audio playback and visualization capabilities within Grasshopper.

## Component Table

| Component Class | Nickname | Category | Description |
| --- | --- | --- | --- |
| `AudioViewerComponent` | Audio Viewer | Playback | Displays and plays audio files with waveform visualization |

## Architecture Notes

- Audio components integrate with the AI audio interaction system
- Support for multiple audio formats (WAV, MP3, MPEG, etc.)
- Waveform visualization for audio content inspection
- Used in conjunction with `Audio2AIComponent` for speech-to-text workflows
- Used with `AI2SpeechComponent` for text-to-speech output playback
