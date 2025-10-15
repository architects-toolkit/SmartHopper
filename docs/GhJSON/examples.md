# GhJSON Examples

## Overview

This document provides practical examples of GhJSON format for common Grasshopper patterns and component types.

---

## Example 1: Simple Addition

A basic addition component with two number inputs.

```json
{
  "components": [
    {
      "name": "Number Slider",
      "type": "IGH_Param",
      "objectType": "Grasshopper.Kernel.Special.GH_NumberSlider",
      "componentGuid": "57da07bd-ecab-415d-9d86-af36d7073abc",
      "instanceGuid": "a1111111-1111-1111-1111-111111111111",
      "selected": false,
      "pivot": {
        "X": 100.0,
        "Y": 100.0
      },
      "properties": {
        "CurrentValue": {
          "value": "5.0<0.0,10.0>",
          "type": "String",
          "humanReadable": "5.0"
        },
        "NickName": {
          "value": "A",
          "type": "String"
        }
      },
      "warnings": [],
      "errors": []
    },
    {
      "name": "Number Slider",
      "type": "IGH_Param",
      "objectType": "Grasshopper.Kernel.Special.GH_NumberSlider",
      "componentGuid": "57da07bd-ecab-415d-9d86-af36d7073abc",
      "instanceGuid": "b2222222-2222-2222-2222-222222222222",
      "selected": false,
      "pivot": {
        "X": 100.0,
        "Y": 150.0
      },
      "properties": {
        "CurrentValue": {
          "value": "3.0<0.0,10.0>",
          "type": "String",
          "humanReadable": "3.0"
        },
        "NickName": {
          "value": "B",
          "type": "String"
        }
      },
      "warnings": [],
      "errors": []
    },
    {
      "name": "Addition",
      "type": "IGH_Component",
      "objectType": "Grasshopper.Kernel.Components.GH_Addition",
      "componentGuid": "a0d62394-a118-422d-abb3-6af115c75b25",
      "instanceGuid": "c3333333-3333-3333-3333-333333333333",
      "selected": false,
      "pivot": {
        "X": 250.0,
        "Y": 125.0
      },
      "properties": {},
      "warnings": [],
      "errors": []
    },
    {
      "name": "Panel",
      "type": "IGH_Param",
      "objectType": "Grasshopper.Kernel.Special.GH_Panel",
      "componentGuid": "59e0b89a-e487-49f8-bab8-b5bab16be14c",
      "instanceGuid": "d4444444-4444-4444-4444-444444444444",
      "selected": false,
      "pivot": {
        "X": 400.0,
        "Y": 125.0
      },
      "properties": {
        "UserText": {
          "value": "",
          "type": "String"
        }
      },
      "warnings": [],
      "errors": []
    }
  ],
  "connections": [
    {
      "from": {
        "instanceId": "a1111111-1111-1111-1111-111111111111",
        "paramName": "Number Slider"
      },
      "to": {
        "instanceId": "c3333333-3333-3333-3333-333333333333",
        "paramName": "A"
      }
    },
    {
      "from": {
        "instanceId": "b2222222-2222-2222-2222-222222222222",
        "paramName": "Number Slider"
      },
      "to": {
        "instanceId": "c3333333-3333-3333-3333-333333333333",
        "paramName": "B"
      }
    },
    {
      "from": {
        "instanceId": "c3333333-3333-3333-3333-333333333333",
        "paramName": "Result"
      },
      "to": {
        "instanceId": "d4444444-4444-4444-4444-444444444444",
        "paramName": "Panel"
      }
    }
  ]
}
```

---

## Example 2: Python Script Component

A Python script component with custom inputs and outputs.

```json
{
  "components": [
    {
      "name": "Python Script",
      "type": "IGH_Component",
      "objectType": "RhinoCodePlatform.GH.Components.PythonScriptComponent",
      "componentGuid": "410755b1-224a-4c1e-a407-bf32fb45ea7e",
      "instanceGuid": "e5555555-5555-5555-5555-555555555555",
      "selected": false,
      "pivot": {
        "X": 200.0,
        "Y": 200.0
      },
      "properties": {
        "Script": {
          "value": "import math\n\nresult = math.sqrt(x ** 2 + y ** 2)\nprint(f'Distance: {result}')",
          "type": "String"
        },
        "ScriptInputs": {
          "value": [
            {
              "variableName": "x",
              "name": "X",
              "description": "X coordinate",
              "access": "item",
              "simplify": false,
              "reverse": false,
              "dataMapping": "None"
            },
            {
              "variableName": "y",
              "name": "Y",
              "description": "Y coordinate",
              "access": "item",
              "simplify": false,
              "reverse": false,
              "dataMapping": "None"
            }
          ],
          "type": "JArray"
        },
        "ScriptOutputs": {
          "value": [
            {
              "variableName": "result",
              "name": "Distance",
              "description": "Euclidean distance",
              "access": "item",
              "simplify": false,
              "reverse": false,
              "dataMapping": "None"
            }
          ],
          "type": "JArray"
        },
        "MarshInputs": {
          "value": true,
          "type": "Boolean"
        },
        "MarshOutputs": {
          "value": true,
          "type": "Boolean"
        },
        "MarshGuids": {
          "value": false,
          "type": "Boolean"
        }
      },
      "warnings": [],
      "errors": []
    }
  ],
  "connections": []
}
```

---

## Example 3: Component with Errors

A component with runtime error messages.

```json
{
  "components": [
    {
      "name": "Division",
      "type": "IGH_Component",
      "objectType": "Grasshopper.Kernel.Components.GH_Division",
      "componentGuid": "c4811991-5c5f-4f61-9882-c7f2e1f3b7a7",
      "instanceGuid": "f6666666-6666-6666-6666-666666666666",
      "selected": true,
      "pivot": {
        "X": 300.0,
        "Y": 150.0
      },
      "properties": {
        "Locked": {
          "value": false,
          "type": "Boolean"
        }
      },
      "warnings": [],
      "errors": [
        "1. Runtime error (ZeroDivisionError): Division by zero"
      ]
    }
  ],
  "connections": []
}
```

---

## Example 4: Parameter with Internalized Data

A number parameter with internalized (persistent) data.

```json
{
  "components": [
    {
      "name": "Number",
      "type": "IGH_Param",
      "objectType": "Grasshopper.Kernel.Parameters.Param_Number",
      "componentGuid": "3581f42a-9592-4549-bd6b-1c0fc39d067b",
      "instanceGuid": "g7777777-7777-7777-7777-777777777777",
      "selected": false,
      "pivot": {
        "X": 150.0,
        "Y": 100.0
      },
      "properties": {
        "PersistentData": {
          "value": {
            "{0}": {
              "0": {"value": 1.5},
              "1": {"value": 2.5},
              "2": {"value": 3.5}
            }
          },
          "type": "JObject"
        },
        "Simplify": {
          "value": false,
          "type": "Boolean"
        },
        "Reverse": {
          "value": false,
          "type": "Boolean"
        }
      },
      "warnings": [],
      "errors": []
    }
  ],
  "connections": []
}
```

---

## Example 5: Value List

A value list component with selectable items.

```json
{
  "components": [
    {
      "name": "Value List",
      "type": "IGH_Param",
      "objectType": "Grasshopper.Kernel.Special.GH_ValueList",
      "componentGuid": "6c24e2c6-02d7-4ada-bcbf-d50ad804d120",
      "instanceGuid": "h8888888-8888-8888-8888-888888888888",
      "selected": false,
      "pivot": {
        "X": 100.0,
        "Y": 200.0
      },
      "properties": {
        "ListMode": {
          "value": "DropDown",
          "type": "String"
        },
        "ListItems": {
          "value": [
            {"Name": "Option A", "Expression": "0"},
            {"Name": "Option B", "Expression": "1"},
            {"Name": "Option C", "Expression": "2"}
          ],
          "type": "JArray"
        },
        "NickName": {
          "value": "Options",
          "type": "String"
        }
      },
      "warnings": [],
      "errors": []
    }
  ],
  "connections": []
}
```

---

## Example 6: AI-Generated with Integer IDs

Components generated by AI using integer IDs (auto-converted to GUIDs).

```json
{
  "components": [
    {
      "name": "Point",
      "componentGuid": "3581f42a-9592-4549-bd6b-1c0fc39d067b",
      "instanceGuid": "1"
    },
    {
      "name": "Circle",
      "componentGuid": "5ec4df20-2f3b-4ae2-a047-b95c9e5c6f3e",
      "instanceGuid": "2"
    },
    {
      "name": "Extrude",
      "componentGuid": "9c2f8e8f-b9b0-4d3e-b0d8-d8e8f8f8f8f8",
      "instanceGuid": "3"
    }
  ],
  "connections": [
    {
      "from": {"instanceId": "1", "paramName": "Point"},
      "to": {"instanceId": "2", "paramName": "Plane"}
    },
    {
      "from": {"instanceId": "2", "paramName": "Circle"},
      "to": {"instanceId": "3", "paramName": "Base"}
    }
  ]
}
```

**Note**: Integer IDs `"1"`, `"2"`, `"3"` will be automatically converted to proper GUIDs during deserialization, maintaining referential integrity in connections.

---

## Example 7: Minimal GhJSON-Lite (Proposed)

Lightweight format for bulk retrieval and analysis.

```json
{
  "components": [
    {
      "name": "Addition",
      "guid": "c3333333-3333-3333-3333-333333333333",
      "type": "a0d62394-a118-422d-abb3-6af115c75b25"
    },
    {
      "name": "Multiplication",
      "guid": "d4444444-4444-4444-4444-444444444444",
      "type": "b4c52c9f-5e8c-4e4e-9c5e-5e8c4e4e9c5e"
    }
  ],
  "connections": [
    {
      "from": "c3333333-3333-3333-3333-333333333333",
      "to": "d4444444-4444-4444-4444-444444444444"
    }
  ]
}
```

---

## Related Documentation

- [GhJSON Format Specification](./format-specification.md)
- [Property Whitelist](./property-whitelist.md)
- [GhJSON Roadmap](./roadmap.md)
