# Limitations

This document outlines the known limitations of SmartHopper. We are continuously working to improve the plugin and address these constraints.

As SmartHopper is currently in active development, limitations and constraints are expected and may change rapidly.

## Known Limitations

- (0.0.0-dev.250122) The state restoration mechanism for `StatefulAsyncComponentBase` is only restored when the component outputs a `GH_Structure`. When the component outputs primitive types directly (such as `int`, `string`, etc.), state is not restored during copy/paste or file open operations.

## Reporting Limitations

If you discover additional limitations or have suggestions for improvements, please:

* Open an [issue on GitHub](https://github.com/architects-toolkit/SmartHopper/issues)
* Provide detailed information about the limitation
* Include any relevant context or use cases
