using System.Collections.Generic;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom
{
    /// <summary>
    /// One RRSK "H" height grid (DevNotes/tools/rrscene/README.md, "H records"): a Unity Terrain's heightmap or the top
    /// surface of a ground mesh that is not in the install, sampled on a regular grid in the scene's own frame.
    /// Heights are absolute scene heights; NaN marks a cell corner with no surface (a mesh grid's holes).
    /// </summary>
    public sealed class RrSceneKitGrid
    {
        /// <summary>The record's id, e.g. "Paintball_River_Terrain#0" or "Paintball_River_Terrain~far#1".</summary>
        public string Id = string.Empty;

        /// <summary>The terrain or mesh name: the id without "#n" and "~far" (a far grid and its near grid share it).</summary>
        public string Name = string.Empty;

        /// <summary>Scene position of corner (0, 0): min x, min height (oy), min z.</summary>
        public Vector3 Origin;

        public float Dx, Dz;
        public int Nx, Nz;

        /// <summary>Nx * Nz absolute heights, row-major (index j * Nx + i at x = ox + i dx, z = oz + j dz).</summary>
        public float[] H;

        public readonly Dictionary<string, string> Keys = new Dictionary<string, string>();

        public bool Collider
        {
            get { return Get("collider") == "1"; }
        }

        public bool Visible
        {
            get { return Get("vis") != "0"; }
        }

        public bool Far
        {
            get { return Get("far") == "1"; }
        }

        /// <summary>"terrain" (a Unity Terrain) or "mesh" (a ground-like mesh).</summary>
        public string Source
        {
            get { return Get("src") ?? "terrain"; }
        }

        /// <summary>The install material the converter named (mat=), '?name' when the level's own is not in the install.</summary>
        public string Material
        {
            get { return Get("mat"); }
        }

        public string Get(string key)
        {
            string v;
            return Keys.TryGetValue(key, out v) ? v : null;
        }

        public float Height(int i, int j)
        {
            return H[j * Nx + i];
        }

        public Vector3 Max
        {
            get { return new Vector3(Origin.x + Dx * (Nx - 1), Origin.y, Origin.z + Dz * (Nz - 1)); }
        }

        /// <summary>True when (x, z) lies inside this grid's rectangle (scene frame).</summary>
        public bool Covers(float x, float z)
        {
            var mx = Max;
            return x >= Origin.x && x <= mx.x && z >= Origin.z && z <= mx.z;
        }

        public override string ToString()
        {
            return Id + " " + Nx + "x" + Nz + " @" + Origin.ToString("F1") + " cell " + Dx.ToString("0.##") + "x" + Dz.ToString("0.##");
        }
    }
}
