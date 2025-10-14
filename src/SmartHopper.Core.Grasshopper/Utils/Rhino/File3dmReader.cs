/*
 * SmartHopper - AI-powered Grasshopper Plugin
 * Copyright (C) 2025 Marc Roca Musach
 *
 * This library is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using RhinoDocObjects = global::Rhino.DocObjects;
using RhinoFileIO = global::Rhino.FileIO;
using RhinoGeometry = global::Rhino.Geometry;

namespace SmartHopper.Core.Grasshopper.Utils.Rhino
{
    /// <summary>
    /// Utilities for reading and analyzing Rhino 3DM files.
    /// </summary>
    public static class File3dmReader
    {
        /// <summary>
        /// Reads a 3DM file and extracts metadata, summary statistics, and layer information.
        /// </summary>
        /// <param name="filePath">Path to the .3dm file.</param>
        /// <param name="includeObjectDetails">Whether to include detailed object information.</param>
        /// <param name="maxObjects">Maximum number of objects to include in details.</param>
        /// <param name="typeFilter">Optional filter for object types.</param>
        /// <returns>JObject containing file analysis results, or null if failed.</returns>
        public static JObject Read3dmFile(
            string filePath,
            bool includeObjectDetails = false,
            int maxObjects = 100,
            HashSet<RhinoDocObjects.ObjectType> typeFilter = null)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                Debug.WriteLine($"[File3dmReader] File not found: {filePath}");
                return null;
            }

            if (!filePath.EndsWith(".3dm", StringComparison.OrdinalIgnoreCase))
            {
                Debug.WriteLine("[File3dmReader] File must be a .3dm file.");
                return null;
            }

            try
            {
                var file3dm = RhinoFileIO.File3dm.Read(filePath);
                if (file3dm == null)
                {
                    Debug.WriteLine($"[File3dmReader] Failed to read 3DM file: {filePath}");
                    return null;
                }

                var result = new JObject();

                // Extract file metadata
                var fileInfo = new FileInfo(filePath);
                result["metadata"] = new JObject
                {
                    ["fileName"] = fileInfo.Name,
                    ["filePath"] = filePath,
                    ["fileSize"] = fileInfo.Length,
                    ["createdDate"] = fileInfo.CreationTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    ["modifiedDate"] = fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    ["unitSystem"] = file3dm.Settings.ModelUnitSystem.ToString()
                };

                // Count objects by type
                var objectCounts = new Dictionary<string, int>();
                var totalObjects = 0;

                foreach (var obj in file3dm.Objects)
                {
                    var typeName = obj.Geometry.ObjectType.ToString();
                    if (!objectCounts.ContainsKey(typeName))
                        objectCounts[typeName] = 0;
                    objectCounts[typeName]++;
                    totalObjects++;
                }

                result["summary"] = new JObject
                {
                    ["totalObjects"] = totalObjects,
                    ["objectsByType"] = JObject.FromObject(objectCounts),
                    ["layerCount"] = file3dm.AllLayers.Count,
                    ["groupCount"] = file3dm.AllGroups.Count,
                    ["materialCount"] = file3dm.AllMaterials.Count
                };

                // Extract layer information
                var layers = new JArray();
                foreach (var layer in file3dm.AllLayers)
                {
                    layers.Add(new JObject
                    {
                        ["name"] = layer.Name,
                        ["fullPath"] = layer.FullPath,
                        ["visible"] = layer.IsVisible,
                        ["locked"] = layer.IsLocked,
                        ["color"] = $"#{layer.Color.R:X2}{layer.Color.G:X2}{layer.Color.B:X2}"
                    });
                }
                result["layers"] = layers;

                // Include detailed object information if requested
                if (includeObjectDetails)
                {
                    var objects = ExtractObjectDetails(file3dm, maxObjects, typeFilter);
                    result["objects"] = objects;
                    result["objectsIncluded"] = objects.Count;
                    result["objectsTotal"] = totalObjects;
                }

                file3dm.Dispose();

                Debug.WriteLine($"[File3dmReader] Successfully analyzed {filePath}: {totalObjects} objects");
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[File3dmReader] Error reading 3DM file: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Extracts detailed information about objects from a 3DM file.
        /// </summary>
        private static JArray ExtractObjectDetails(RhinoFileIO.File3dm file3dm, int maxObjects, HashSet<RhinoDocObjects.ObjectType> typeFilter)
        {
            var objects = new JArray();
            var count = 0;

            foreach (var obj in file3dm.Objects)
            {
                if (count >= maxObjects)
                    break;

                // Apply type filter if specified
                if (typeFilter != null && !typeFilter.Contains(obj.Geometry.ObjectType))
                    continue;

                var objInfo = new JObject
                {
                    ["id"] = obj.Id.ToString(),
                    ["type"] = obj.Geometry.ObjectType.ToString(),
                    ["layer"] = obj.Attributes.LayerIndex >= 0 && obj.Attributes.LayerIndex < file3dm.AllLayers.Count
                        ? file3dm.AllLayers.FindIndex(obj.Attributes.LayerIndex).Name
                        : "Unknown",
                    ["name"] = obj.Attributes.Name ?? string.Empty,
                    ["visible"] = obj.Attributes.Visible
                };

                // Add geometry-specific information
                var geometry = obj.Geometry;
                if (geometry != null)
                {
                    var bbox = geometry.GetBoundingBox(false);
                    objInfo["boundingBox"] = new JObject
                    {
                        ["min"] = new JArray { bbox.Min.X, bbox.Min.Y, bbox.Min.Z },
                        ["max"] = new JArray { bbox.Max.X, bbox.Max.Y, bbox.Max.Z }
                    };

                    AddGeometryTypeDetails(objInfo, geometry);
                }

                objects.Add(objInfo);
                count++;
            }

            return objects;
        }

        /// <summary>
        /// Adds type-specific geometry details to the object info.
        /// </summary>
        private static void AddGeometryTypeDetails(JObject objInfo, RhinoGeometry.GeometryBase geometry)
        {
            switch (geometry.ObjectType)
            {
                case RhinoDocObjects.ObjectType.Point:
                    var pt = geometry as RhinoGeometry.Point;
                    if (pt != null)
                    {
                        objInfo["location"] = new JArray { pt.Location.X, pt.Location.Y, pt.Location.Z };
                    }
                    break;

                case RhinoDocObjects.ObjectType.Curve:
                    var curve = geometry as RhinoGeometry.Curve;
                    if (curve != null)
                    {
                        objInfo["length"] = curve.GetLength();
                        objInfo["isClosed"] = curve.IsClosed;
                        objInfo["degree"] = curve.Degree;
                    }
                    break;

                case RhinoDocObjects.ObjectType.Surface:
                case RhinoDocObjects.ObjectType.Brep:
                    var brep = geometry as RhinoGeometry.Brep;
                    if (brep != null)
                    {
                        objInfo["faceCount"] = brep.Faces.Count;
                        objInfo["edgeCount"] = brep.Edges.Count;
                        objInfo["isSolid"] = brep.IsSolid;
                        if (brep.IsSolid)
                        {
                            objInfo["volume"] = brep.GetVolume();
                        }
                    }
                    break;

                case RhinoDocObjects.ObjectType.Mesh:
                    var mesh = geometry as RhinoGeometry.Mesh;
                    if (mesh != null)
                    {
                        objInfo["vertexCount"] = mesh.Vertices.Count;
                        objInfo["faceCount"] = mesh.Faces.Count;
                        objInfo["isClosed"] = mesh.IsClosed;
                    }
                    break;
            }
        }
    }
}
