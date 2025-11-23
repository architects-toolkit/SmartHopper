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
using System.Linq;
using Newtonsoft.Json.Linq;
using RhinoApp = global::Rhino.RhinoDoc;
using RhinoDocObjects = global::Rhino.DocObjects;
using RhinoGeometry = global::Rhino.Geometry;

namespace SmartHopper.Core.Grasshopper.Utils.Rhino
{
    /// <summary>
    /// Utilities for extracting geometry information from the active Rhino document.
    /// </summary>
    public static class DocumentGeometryExtractor
    {
        /// <summary>
        /// Gets geometry information from the active Rhino document based on filter criteria.
        /// </summary>
        /// <param name="filter">Filter mode: "selected", "all", "layer", "type".</param>
        /// <param name="layerName">Layer name (required when filter="layer").</param>
        /// <param name="objectType">Object type (required when filter="type").</param>
        /// <param name="includeDetails">Whether to include detailed geometry data.</param>
        /// <param name="maxObjects">Maximum number of objects to return.</param>
        /// <returns>JObject containing geometry data, or null if failed.</returns>
        public static JObject GetGeometry(
            string filter = "selected",
            string layerName = null,
            RhinoDocObjects.ObjectType? objectType = null,
            bool includeDetails = false,
            int maxObjects = 50)
        {
            var doc = RhinoApp.ActiveDoc;
            if (doc == null)
            {
                Debug.WriteLine("[DocumentGeometryExtractor] No active Rhino document found.");
                return null;
            }

            // Get objects based on filter
            List<RhinoDocObjects.RhinoObject> objects = GetFilteredObjects(doc, filter, layerName, objectType);
            if (objects == null || !objects.Any())
            {
                Debug.WriteLine($"[DocumentGeometryExtractor] No objects found matching filter: {filter}");
                return null;
            }

            // Limit results
            var limitedObjects = objects.Take(maxObjects).ToList();

            // Extract geometry information
            var geometryData = new JArray();
            foreach (var obj in limitedObjects)
            {
                var objData = ExtractObjectGeometry(obj, doc, includeDetails);
                if (objData != null)
                {
                    geometryData.Add(objData);
                }
            }

            var result = new JObject
            {
                ["objects"] = geometryData,
                ["count"] = geometryData.Count,
                ["totalFound"] = objects.Count,
                ["filter"] = filter
            };

            if (objects.Count > maxObjects)
            {
                result["message"] = $"Showing {maxObjects} of {objects.Count} objects. Increase maxObjects parameter to see more.";
            }

            Debug.WriteLine($"[DocumentGeometryExtractor] Retrieved {geometryData.Count} objects with filter: {filter}");
            return result;
        }

        /// <summary>
        /// Gets objects from the document based on filter criteria.
        /// </summary>
        private static List<RhinoDocObjects.RhinoObject> GetFilteredObjects(
            RhinoApp doc,
            string filter,
            string layerName,
            RhinoDocObjects.ObjectType? objectType)
        {
            switch (filter.ToLower())
            {
                case "selected":
                    return doc.Objects.GetSelectedObjects(false, false).ToList();

                case "all":
                    return doc.Objects.Where(obj => obj.IsValid && !obj.IsDeleted).ToList();

                case "layer":
                    if (string.IsNullOrEmpty(layerName))
                    {
                        Debug.WriteLine("[DocumentGeometryExtractor] layerName is required when filter='layer'.");
                        return null;
                    }

                    var layer = doc.Layers.FindName(layerName);
                    if (layer == null)
                    {
                        Debug.WriteLine($"[DocumentGeometryExtractor] Layer not found: {layerName}");
                        return null;
                    }

                    return doc.Objects.FindByLayer(layer).Where(obj => obj.IsValid && !obj.IsDeleted).ToList();

                case "type":
                    if (!objectType.HasValue)
                    {
                        Debug.WriteLine("[DocumentGeometryExtractor] objectType is required when filter='type'.");
                        return null;
                    }

                    return doc.Objects.Where(obj => obj.IsValid && !obj.IsDeleted && obj.ObjectType == objectType.Value).ToList();

                default:
                    Debug.WriteLine($"[DocumentGeometryExtractor] Invalid filter mode: {filter}");
                    return null;
            }
        }

        /// <summary>
        /// Extracts geometry information from a single Rhino object.
        /// </summary>
        private static JObject ExtractObjectGeometry(RhinoDocObjects.RhinoObject obj, RhinoApp doc, bool includeDetails)
        {
            var objData = new JObject
            {
                ["id"] = obj.Id.ToString(),
                ["type"] = obj.ObjectType.ToString(),
                ["layer"] = obj.Attributes.LayerIndex >= 0 ? doc.Layers[obj.Attributes.LayerIndex].Name : "Unknown",
                ["name"] = obj.Attributes.Name ?? string.Empty,
                ["visible"] = obj.Visible,
                ["locked"] = obj.IsLocked
            };

            var geometry = obj.Geometry;
            if (geometry == null)
                return objData;

            // Bounding box
            var bbox = geometry.GetBoundingBox(false);
            objData["boundingBox"] = new JObject
            {
                ["min"] = new JArray { bbox.Min.X, bbox.Min.Y, bbox.Min.Z },
                ["max"] = new JArray { bbox.Max.X, bbox.Max.Y, bbox.Max.Z },
                ["center"] = new JArray { bbox.Center.X, bbox.Center.Y, bbox.Center.Z }
            };

            // Type-specific information
            AddGeometryTypeInfo(objData, geometry, includeDetails);

            return objData;
        }

        /// <summary>
        /// Adds type-specific geometry information to the object data.
        /// </summary>
        private static void AddGeometryTypeInfo(JObject objData, RhinoGeometry.GeometryBase geometry, bool includeDetails)
        {
            switch (geometry.ObjectType)
            {
                case RhinoDocObjects.ObjectType.Point:
                    var pt = geometry as RhinoGeometry.Point;
                    if (pt != null)
                    {
                        objData["location"] = new JArray { pt.Location.X, pt.Location.Y, pt.Location.Z };
                    }
                    break;

                case RhinoDocObjects.ObjectType.Curve:
                    var curve = geometry as RhinoGeometry.Curve;
                    if (curve != null)
                    {
                        objData["length"] = curve.GetLength();
                        objData["isClosed"] = curve.IsClosed;
                        objData["isPlanar"] = curve.IsPlanar();
                        objData["degree"] = curve.Degree;
                        objData["domain"] = new JObject
                        {
                            ["min"] = curve.Domain.Min,
                            ["max"] = curve.Domain.Max
                        };

                        if (includeDetails)
                        {
                            objData["startPoint"] = new JArray { curve.PointAtStart.X, curve.PointAtStart.Y, curve.PointAtStart.Z };
                            objData["endPoint"] = new JArray { curve.PointAtEnd.X, curve.PointAtEnd.Y, curve.PointAtEnd.Z };

                            var nurbsCurve = curve.ToNurbsCurve();
                            if (nurbsCurve != null)
                            {
                                var controlPoints = new JArray();
                                foreach (var cp in nurbsCurve.Points)
                                {
                                    controlPoints.Add(new JArray { cp.X, cp.Y, cp.Z, cp.Weight });
                                }
                                objData["controlPoints"] = controlPoints;
                            }
                        }
                    }
                    break;

                case RhinoDocObjects.ObjectType.Surface:
                case RhinoDocObjects.ObjectType.Brep:
                    var brep = geometry as RhinoGeometry.Brep;
                    if (brep != null)
                    {
                        objData["faceCount"] = brep.Faces.Count;
                        objData["edgeCount"] = brep.Edges.Count;
                        objData["vertexCount"] = brep.Vertices.Count;
                        objData["isSolid"] = brep.IsSolid;
                        objData["isClosed"] = brep.IsSolid;

                        if (brep.IsSolid)
                        {
                            objData["volume"] = brep.GetVolume();
                        }

                        var area = RhinoGeometry.AreaMassProperties.Compute(brep);
                        if (area != null)
                        {
                            objData["area"] = area.Area;
                            objData["centroid"] = new JArray { area.Centroid.X, area.Centroid.Y, area.Centroid.Z };
                        }
                    }
                    break;

                case RhinoDocObjects.ObjectType.Mesh:
                    var mesh = geometry as RhinoGeometry.Mesh;
                    if (mesh != null)
                    {
                        objData["vertexCount"] = mesh.Vertices.Count;
                        objData["faceCount"] = mesh.Faces.Count;
                        objData["isClosed"] = mesh.IsClosed;
                        objData["isValid"] = mesh.IsValid;

                        if (includeDetails)
                        {
                            var vertices = new JArray();
                            var maxVerts = Math.Min(mesh.Vertices.Count, 1000); // Limit to 1000 vertices
                            for (int i = 0; i < maxVerts; i++)
                            {
                                var v = mesh.Vertices[i];
                                vertices.Add(new JArray { v.X, v.Y, v.Z });
                            }
                            objData["vertices"] = vertices;
                            objData["verticesIncluded"] = maxVerts;
                        }
                    }
                    break;
            }
        }
    }
}
