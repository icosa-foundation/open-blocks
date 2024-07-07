// Modified from original: https://github.com/n-yoda/unity-off
//
// Zlib Licenced:
//
// Copyright (c) 2014 n-yoda
// This software is provided 'as-is', without any express or implied
// warranty. In no event will the authors be held liable for any damages
// arising from the use of this software.
//
//     Permission is granted to anyone to use this software for any purpose,
//     including commercial applications, and to alter it and redistribute it
//     freely, subject to the following restrictions:
//
// 1. The origin of this software must not be misrepresented; you must not
//     claim that you wrote the original software. If you use this software
//     in a product, an acknowledgment in the product documentation would be
//     appreciated but is not required.
//
// 2. Altered source versions must be plainly marked as such, and must not be
//    misrepresented as being the original software.
//
// 3. This notice may not be removed or altered from any source distribution.

using UnityEngine;
using System.Collections.Generic;
using System.IO;
using com.google.apps.peltzer.client.model.core;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using com.google.apps.peltzer.client.model.render;

namespace com.google.apps.peltzer.client.model.import
{
    /// <summary>
    /// Imports obj and mtl files.
    /// </summary>
    public static class OffImporter
    {
        private static MMesh mmesh;

        const string PrefixNormal = "N";
        const string PrefixColor = "C";
        const string PrefixTextureCoordinate = "ST";
        const string PrefixNDimension = "n";
        const string PrefixHomogeneousCoordinate = "4";

        private static float defaultScale = 0.1f;

        /// <summary>
        ///   Creates an MMesh from the contents of a .off file with the given id.
        /// </summary>
        /// <param name="offFileContents">The contents of a .off file.</param>
        /// <param name="id">The id of the new MMesh.</param>
        /// <param name="result">The created mesh, or null if it could not be created.</param>
        /// <returns>Whether the MMesh could be created.</returns>
        public static bool MMeshFromOffFile(string offFileContents, int id, out MMesh result)
        {
            var success = OffToMMesh(id, offFileContents);
            if (success)
            {
                result = mmesh;
                return true;
            }
            result = null;
            return false;
        }

        public static bool OffToMMesh(int id, string offFileContents)
        {
            var tokens = getTokensOfNonEmptyLines(new StringReader(offFileContents));
            mmesh = null;
            var parser = parseOff(id);
            while (parser.MoveNext())
            {
                if (tokens.MoveNext())
                    parser.Current(tokens.Current);
                else
                    return false;
            }
            return true;
        }

        static IEnumerator<string[]> getTokensOfNonEmptyLines(TextReader off)
        {
            var re = new Regex(@"\s+");
            while (off.Peek() > 0)
            {
                var line = off.ReadLine();
                var sharp = line.IndexOf("#");
                if (sharp >= 0)
                {
                    line = line.Substring(0, sharp);
                }
                line = line.Trim(" \t\n\r".ToCharArray());
                if (line.Length > 0) yield return re.Split(line);
            }
        }

        static IEnumerator<Action<string[]>> parseOff(int id)
        {
            var hasNormal = false;
            var hasColor = false;
            var hasUv = false;
            var hasHomo = false;
            var hasDim = false;
            var dim = 3;

            var vertexCount = 0;
            var faceCount = 0;

            // Parse Header
            yield return tokens =>
            {
                if (tokens.Length != 1)
                    throw new Exception("Invalid OFF header: ");
                var re = new Regex("(?<ST>ST)?(?<C>C)?(?<N>N)?(?<4>4)?(?<n>n)?OFF");
                var match = re.Match(tokens[0]);
                if (!match.Success)
                    throw new Exception("Invalid OFF header.");

                hasNormal = match.Groups[PrefixNormal].Value == PrefixNormal;
                hasColor = match.Groups[PrefixColor].Value == PrefixColor;
                hasUv = match.Groups[PrefixTextureCoordinate].Value == PrefixTextureCoordinate;
                hasHomo = match.Groups[PrefixHomogeneousCoordinate].Value == PrefixHomogeneousCoordinate;
                hasDim = match.Groups[PrefixNDimension].Value == PrefixNDimension;
            };

            // Dimension
            if (hasDim)
            {
                yield return tokens =>
                {
                    if (tokens.Length != 1
                        || !int.TryParse(tokens[0], out dim)
                        || dim > 3)
                    {
                        throw new Exception("Dimension should not be more than 3.");
                    }
                };
            }

            // Counts
            yield return tokens =>
            {
                if (!int.TryParse(tokens[0], out vertexCount)
                    || !int.TryParse(tokens[1], out faceCount))
                    throw new Exception("Invalid vertex or face count.");
            };

            // Vertex

            var vertices = new List<Vector3>(vertexCount);
            var normals = hasNormal ? new List<Vector3>(vertexCount) : null;
            var colors = hasColor ? new List<Color>(vertexCount) : null;
            var colorsPerFace = new List<Color>(faceCount);
            var faceProperties = new List<FaceProperties>();
            var uvs = hasUv ? new List<Vector2>(vertexCount) : null;
            var faces = new List<List<int>>(faceCount);

            var normOff = hasHomo ? dim + 1 : dim;
            var colOff = hasNormal ? normOff + dim : normOff;
            var uvOff = hasColor ? colOff + 4 : colOff;

            Action<string[]> parseVert = tokens =>
            {
                var w = 1f;
                var pos = new Vector3();
                var normal = new Vector3();
                var color = new Color();
                var uv = new Vector2();
                if ((dim > 0 && !float.TryParse(tokens[0], out pos.x)) ||
                    (dim > 1 && !float.TryParse(tokens[1], out pos.y)) ||
                    (dim > 2 && !float.TryParse(tokens[2], out pos.z)) ||
                    (hasHomo && !float.TryParse(tokens[dim], out w)) ||
                    (hasNormal && !(
                        float.TryParse(tokens[normOff + 0], out normal.x) &&
                        float.TryParse(tokens[normOff + 1], out normal.y) &&
                        float.TryParse(tokens[normOff + 2], out normal.z))) ||
                    (hasColor && !(
                        float.TryParse(tokens[colOff + 0], out color.r) &&
                        float.TryParse(tokens[colOff + 1], out color.g) &&
                        float.TryParse(tokens[colOff + 2], out color.b) &&
                        float.TryParse(tokens[colOff + 3], out color.a))) ||
                    (hasUv && !(
                        float.TryParse(tokens[uvOff + 0], out uv.x) &&
                        float.TryParse(tokens[uvOff + 1], out uv.y))))
                {
                    throw new Exception($"Vertex Parse error: {tokens}");
                }

                if (hasHomo) pos /= w;
                vertices.Add(pos * defaultScale);
                if (hasNormal) normals.Add(normal);
                if (hasUv) uvs.Add(uv);
                if (hasColor)
                {
                    colors.Add(color);
                }
            };
            for (int i = 0; i < vertexCount; i++)
            {
                yield return parseVert;
            }

            // Indexes
            Action<string[]> parseFace = tokens =>
            {
                int numSides = int.Parse(tokens[0]);
                var faceVertexIndices = tokens.Skip(1).Take(numSides).Select(int.Parse).ToList();
                faces.Add(faceVertexIndices);
                if (tokens.Length >= numSides + 4)
                {
                    var colValues = tokens.Skip(numSides + 1).Take(3).Select(float.Parse).ToList();
                    colorsPerFace.Add(new Color(colValues[0], colValues[1], colValues[2]));
                }
                else if (hasColor)
                {
                    var faceColors = colors.Skip(colors.Count - faceVertexIndices.Count);
                    var mostPopularColor = faceColors
                        .GroupBy(color => color)
                        .OrderByDescending(group => group.Count())
                        .First()
                        .Key;
                    colorsPerFace.Add(mostPopularColor);
                }
                else
                {
                    colorsPerFace.Add(Color.white);
                }
            };

            for (int i = 0; i < faceCount; i++)
            {
                yield return parseFace;
            }

            faceProperties = colorsPerFace.Select(color => new FaceProperties(MaterialRegistry.GetMaterialIdClosestToColor(color))).ToList();

            mmesh = new MMesh(id, Vector3.zero, Quaternion.identity, vertices, faces, faceProperties);
        }
    }
}
