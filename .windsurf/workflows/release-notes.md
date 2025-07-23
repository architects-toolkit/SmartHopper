---
description: Write the release notes
---

The aim is to return the release notes to publish on GitHub.

1. Analyze the last release in CHANGELOG.md

If last release is a major release (1.0.0) or a minor release (0.1.0), follow the following instructions. If it is a patch release (0.0.1) jump to step 3.

2. This step if for major release (1.0.0) and minor release (0.1.0) only:

2.1. Return a title for the release in a code block, in plain text, with this format: "SmartHopper X.X.X(-alpha): Main Release Change". Examples: SmartHopper 0.3.2-alpha: Script Components Unleashed,  SmartHopper 0.3.1-alpha: Tidy Up Your File!, SmartHopper 0.3.0-alpha: Powerful AI tools and enhanced security

2.2. Return the release notes in a markdown code block, following this format:

Brief sentence summarizing the release. (e.g. This alpha release packs powerful undo support, new scripting tools to create and review scripting components, and a sweeping refactor of our AI workflows in Grasshopper—plus fresh branding and quality-of-life fixes.)

## (emoji) Feature 1

Brief description of the feature, changes from previous releases... focusing on perceptible changes for the user (UI xperience, new/removed inputs/outputs in components, new/removed components...).

## (emoji) Feature 2

Repeat features as necessary for the release.

## 🛠️ Technical Requirements

- Rhino 8.19 or above is required
- Windows 10/11 (MacOS has not been tested)
- Valid API keys for MistralAI, OpenAI or DeepSeek

## ⚠️ Important Notes

- This is an alpha release with some features still unstable or subject to change
- API keys are required, and usage costs apply based on your provider
- Documentation is currently under development

## 🤝 We Value Your Feedback!

Help shape SmartHopper's future by:
- Sharing your experiences with the new features
- Suggesting improvements via our [discussion](https://github.com/architects-toolkit/SmartHopper/discussions)
- Telling us what AI capabilities would help your workflow most

We hope you enjoy these new features and improvements!

Happy designing! 🎨

---

3. This step is for patch release (0.0.1) only:

3.1. Return a title for the release in a code block, in plain text, with this format: "SmartHopper X.X.X(-alpha): Main Release Change [Patch]".

3.2. Return the release notes in a markdown code block, following this format:

Brief sentence summarizing the release. (e.g. This patch release fixes XX issues concerning... This patch release adds a missing icon for XX component...)

## Detailed list of changes: //read the changelog to fill this part. Summarize the changelog, do not copy-paste it literally//

- Added a new ...
- Removed ...
- Fixed issue [#00](link-to-issue)
