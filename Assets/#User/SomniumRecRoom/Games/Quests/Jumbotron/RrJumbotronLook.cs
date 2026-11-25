using System;
using System.Globalization;
using UnityEngine;

namespace SomniumSpace.Worlds.SomniumRecRoom.Games.Quests.Jumbotron
{
    /// <summary>
    /// How each level material of a Jumbotron room is drawn on <see cref="RrJumbotronHull"/>'s plates and dress boxes - the
    /// Golden Trophy's approach (RrGoldenTrophyHull.Plan) with the numbers measured for these levels (DevNotes/iter10/
    /// jt_materials.md section 5, DevNotes/iter11/materials.md). Every surface is an INSTALL AG/Standard-* material (the
    /// shaders RrLightGrid lights): the level's own material when the install has it, else the closest install material as
    /// a template, cloned with the install texture, tint and glow the table gives.
    ///
    /// Rec Room's AG/Standard-* shaders (install disassembly, jt_materials 2 / jt_lighting F):
    /// <code>
    /// col    = meshVertexColour.rgb * _BaseColor            (the vertex colour RAW, _BaseColor sRGB -> linear)
    /// albedo = lerp(tex.rgb, tex.rgb * col, tex.a)           (the base texture's ALPHA masks the tint)
    /// emit   = tex.rgb * tex.a * col * _EmissivePower        (and the glow)
    /// </code>
    /// So the plates' c= tints go into the mesh colours, never into _BaseColor, and _BaseColor alpha (0.354 on the opaque
    /// sci-fi materials) is NOT transparency: only the real glass is glass (<see cref="SeeThrough"/> guards every template).
    ///
    /// Iteration 11 (the headset test: "wrong textures"): the level textures were decoded next to their substitutes
    /// (look-only scratch sheets). Several of Rec Room's sci-fi textures are ATLASES that the level meshes' own UVs cut
    /// up (the door panel: a door face, hazard stripes, a white corner and one glow strip; the curtain: cloth, rope and a
    /// gold fringe; the rug: one rug picture). Our plates have world-projected UVs, so an atlas tiled over them is noise -
    /// hazard stripes and icons repeated up the pillars, white or glowing-cyan patches on door frames. Those materials are
    /// now PLAIN surfaces at Rec Room's measured mean colour (<see cref="Plain"/>: the dull-metal template when the tint
    /// fits, else the paint template): a plain panel reads better than a wrong pattern. Every opaque AG material in Rec
    /// Room is back-face culled (shader pass state, constant Cull Back), so no hull material is double-sided.
    ///
    /// Tiling: our plate UVs are world metres; <see cref="Look.TileU"/> is metres per texture repeat
    /// (= 1 / (the level mesh's measured UV density x the level material's scale), jt_materials 4c.4). On walls the plate
    /// u is horizontal and v world up (gtscene's rectangle axes = ProBuilder's planar UVs).
    /// Smoothness is not set: these shaders take it from their _SpecGlossMap, not from a material float.
    /// </summary>
    public static class RrJumbotronLook
    {
        /// <summary>Linear mean of the install Paint_Tex (the paint template Paint_LightGrey_Mat's texture).</summary>
        public const float PaintTexLinear = 0.61f;

        /// <summary>Linear mean of the install Metal_Tex @1df8e0ef (Metal_LightGrey_Dull_Mat's texture, sRGB mean 0.384).</summary>
        public const float MetalTexLinear = 0.1221f;

        public const string Concrete = "Concrete_Blue_50x_Mat";
        public const string Door = "Door_SciFi_Cardboard_mat";
        public const string Pen = "Paint_LightGrey_Mat";
        public const string Metal = "Metal_LightGrey_Dull_Mat";
        public const string Planks = "Wood_Floor_Matte_mat";
        public const string GlassTemplate = "Glass_Teal_mat";

        /// <summary>How one level material is drawn.</summary>
        public sealed class Look
        {
            /// <summary>Material cache key (the level material, plus the colour when the look depends on it).</summary>
            public string Key;
            /// <summary>Install material: used as it is, or as the template of a clone.</summary>
            public string Template;
            /// <summary>Install textures put on the clone (_BaseMap / _BumpMap / _SpecGlossMap; null = the template's own).</summary>
            public string Texture, Bump, Spec;
            public bool SetTint;
            /// <summary>_BaseColor as an sRGB colour (SetColor); alpha only used for <see cref="Glass"/>.</summary>
            public Color Tint = Color.white;
            /// <summary>_EmissivePower (below 0 = the template's).</summary>
            public float Glow = -1f;
            /// <summary>Metres per texture repeat along the plate's u / v (v 0 = the same as u).</summary>
            public float TileU = 1f, TileV;
            /// <summary>A real transparent material (the level shader is AG/Standard-Transparent*): the only glass.</summary>
            public bool Glass;
            /// <summary>Not drawn (decals, glow cards: their textures are level-only; opaque rectangles otherwise).</summary>
            public bool Skip;
            /// <summary>Dress only: UVs 0..1 per box face (a picture, the marquee) instead of metres.</summary>
            public bool FaceUv;
            /// <summary>Plates: the texture fitted 0..1 over each plate (a picture: the rugs) instead of world tiling.</summary>
            public bool Fit;
            /// <summary>The texture turned 90 degrees on the plate (texture v along the plate's u: the ribbed walls).</summary>
            public bool Rotate;
            /// <summary>Plates: drawn as the 80s striped sunset disc (vertex-colour gradient), not as the plate's square.</summary>
            public bool Disc;
            /// <summary>The level material's own install material, as it is.</summary>
            public bool AsIs;

            public float V
            {
                get { return TileV > 0f ? TileV : TileU; }
            }

            /// <summary>Whether the template must be cloned (anything but tiling differs from it; the glass template is used as it is unless tinted).</summary>
            public bool NeedsClone
            {
                get { return SetTint || Glow >= 0f || Texture != null || Bump != null || Spec != null; }
            }

            public string Describe()
            {
                if (Skip)
                {
                    return "skip";
                }

                string s = Template ?? "-";
                if (Texture != null)
                {
                    s += "+" + Texture;
                }

                if (Bump != null)
                {
                    s += "+" + Bump;
                }

                if (SetTint)
                {
                    s += " tint " + ColorUtility.ToHtmlStringRGB(Tint);
                }

                if (Glow >= 0f)
                {
                    s += " glow " + Glow.ToString("0.##", CultureInfo.InvariantCulture);
                }

                if (Glass)
                {
                    s += " glass";
                }

                if (Fit)
                {
                    return s + " fit";
                }

                return s + (Rotate ? " rot" : "") + " tile " + TileU.ToString("0.###", CultureInfo.InvariantCulture) + (TileV > 0f && Math.Abs(TileV - TileU) > 1e-4f ? "x" + TileV.ToString("0.###", CultureInfo.InvariantCulture) : "") + "m";
            }
        }

        /// <summary>
        /// The look of a level material. col / tex / scale / inst are the level's platemat (or dress) values; dress =
        /// for a stand-in box (the cover walls' door-panel picture is fitted to their ~1.3 m faces).
        /// </summary>
        public static Look Plan(string mat, Color col, Vector2 scale, bool inst, bool dress)
        {
            string l = (mat ?? string.Empty).ToLowerInvariant();
            switch (l)
            {
                // ---- the ~18 materials that make 94 % of the area (jt_materials 5, top rows)
                case "wall_vertcolor_mat":
                    // Concrete_Tex x25 + Concrete_Norm/Spec: the template IS the level's textures; vc = the room colour.
                    return On(mat, Concrete, Color.white, 0.04f);
                case "cinderblock_vertcolor_mat":
                    // Brick_Tex is level-only: Concrete_Tex has its mean and alpha (the vc tint applies fully); the
                    // cinderblock relief is the level's own Brick_Norm / Brick_Spec, which the install has (same layout).
                    return Brick(On(mat, Concrete, Color.white, 2f));
                case "brick_painted_lightgrey_mat":
                    return Brick(On(mat, Concrete, Grey(0.985f), 2f));
                case "brick_painted_grey_mat":
                    return Brick(On(mat, Concrete, Grey(0.666f), 2f));
                case "paint_darkgrey_mat":
                    return As(mat, 0.02f);
                case "linoleum_flecked_brown_mat":
                case "linoleum_flecked_brown_mat_floorqueue":
                    // Linoleum_Flecked_Tex = the install's Tile_Linoleum_Flecked_Tex, which holds it 2 x 2 (with tile
                    // seams): 2 m per repeat keeps the level's 1 m fleck scale. Measured tint.
                    return On(mat, "Tile_Linoleum_Brown_Mat", new Color(0.585f, 0.554f, 0.536f), 2f);
                case "concrete_lightgrey_2x_mat":
                    return As(mat, 0.5f);
                case "unlit_black_mat":
                    return As(mat, 1f);
                case "metal_corrugated_blue_mat":
                    // The level's own texture (horizontal ribs on metre UVs, u 1 m x v 2 m).
                    return On(mat, "Metal_StepTrim_Mat", new Color(0.085f, 0.334f, 0.462f), 1f, 2f);
                case "concrete_vertcolor_mat":
                    // = Concrete_LightGrey_2x_Mat (same textures and colour) + vertex colour: the install material as it is.
                    return Use(mat, "Concrete_LightGrey_2x_Mat", 0.5f);
                case "concrete_grey_3x_mat":
                    return On(mat, Concrete, Grey(0.824f), 0.33f);
                case "metal_dull_dark_mat":
                    return As(mat, 0.25f);
                case "wallpaper_scifipanels_big_mat":
                    // Wallpaper_SciFiPanels_Tex is level-only (bold 5 m panels): the install Metal_Tex has its mean (0.38)
                    // and alpha; no install panel texture tiles (the door panel is an atlas).
                    return Use(mat, Metal, 1f);
                case "wood_table_mat":
                    return As(mat, 25f, 16.7f);
                case "metal_lightgrey_dull_mat":
                    return As(mat, 12.5f);
                case "door_scifi_cardboard_mat":
                    // An ATLAS (door face, hazard stripes, a white corner, one glow strip in alpha): on a stand-in box's
                    // faces it is the sci-fi door picture; world-tiled over the hull's door frames and panels it put
                    // random stripes, icons and glowing cyan squares everywhere -> plain metal at Rec Room's mean.
                    return dress ? As(mat, 1.5f) : Plain(mat, new Color(0.351f, 0.366f, 0.363f));
                case "pillar_scifi_pink_mat":
                case "pillar_scifi_red_mat":
                    // Pillar_SciFi_tex is a plain grey panel (0.38) with a few tiny glow dots (alpha 0.011): grey metal.
                    // (Iteration 10 tiled the door atlas up them: hazard stripes and door icons on every pillar.)
                    return Plain(mat, new Color(0.381f, 0.372f, 0.378f));
                case "concrete_paleblue_200x_mat":
                    return On(mat, Concrete, new Color(0.734f, 0.884f, 1f), 0.25f);
                case "metal_floortread_mat":
                    // The install's tread plate (the same diamond pattern and density); tint from the measured means.
                    return On(mat, "Metal_FloorTread_Shiny_Mat", new Color(0.688f, 0.731f, 0.734f), 1f);

                // ---- the rest of the plates
                case "wood_floor_matte_mat":
                    return As(mat, 5.6f, 1.4f);
                case "plastic_grey_gloss_mat":
                    return As(mat, 1.8f);
                case "curtain_black_mat":
                    // Curtains_Tex is an atlas (cloth, a rope strip, a gold fringe): the cloth alone, 0.932 x 0.245 = a
                    // plain dark cloth (world-tiled, the fringe and rope showed as gold stripes across the curtains).
                    return Mean(mat, Grey(0.225f));
                case "stagetop_mat":
                    // StageTop_tex is grey planks (~0.2 m boards): the install's plank floor, Rec Room's mean colour.
                    return On(mat, Planks, new Color(0.405f, 0.27f, 0.069f), 1f);
                case "wood_floor_wide_mat":
                    return On(mat, Planks, new Color(0.578f, 0.46f, 0.395f), 5f, 1.33f);
                case "metal_dull_mat":
                    return On(mat, Metal, Grey(0.596f), dress ? 1f : 0.31f);
                case "scifi_rug_mat":
                    // One rug picture (dark border, blue-grey centre), ~one repeat per rug in the level: fitted per plate.
                    return Fitted(As(mat, 4.3f));
                case "metal_ribbed_gray_mat":
                    // Metal_Ribbed_Tex (level-only) has VERTICAL ribs every 0.25 m; the install's corrugated texture runs
                    // across v, so it is turned: 6 ridges per 1.5 m along the plate's u. Measured tint.
                    return Turned(On(mat, "Metal_StepTrim_Mat", new Color(0.527f, 0.57f, 0.599f), 1.5f, 1f));
                case "vent_modular_mat":
                    return As(mat, dress ? 1f : 2.3f);
                case "carpet_blacklight_swirls_mat":
                {
                    // Armory blacklight carpet: its texture is level-only; the install's sci-fi carpet has the same swirls.
                    var k = Glowing(On(mat, "Scifi_Rug_Mat", Color.white, 2f), 0.3f);
                    k.Texture = "SciFi_Carpet_Tex";
                    return k;
                }
                case "scifi_doortrigger_mat":
                    // DoorTrigger_Tex is an atlas (grey panels, a glowing diamond, a hazard corner): world-tiled it put
                    // stripes and diamonds at random; Rec Room's mean on the dull metal.
                    return Plain(mat, new Color(0.283f, 0.255f, 0.246f));
                case "poster_shakehands_mat":
                    // A poster picture: fitted to its plate.
                    return Fitted(As(mat, 1f));
                case "lockerfountainstagelight_mat":
                    // A prop atlas (lockers, fountain, stage-light cans): as it is on the level's plates; plain dark metal
                    // on the stand-in boxes and the tripod frames.
                    return dress ? Plain(mat, new Color(0.3f, 0.32f, 0.35f)) : As(mat, 1f);
                case "80sunset_mat":
                    // The 80s sunset over the space walk (an 11 m square; Sunset_Tex, level-only, is a striped disc glowing
                    // yellow to magenta, transparent around it): drawn as that disc (RrJumbotronHull.Sunset), the stripes
                    // and gradient in vertex colours on the emissive template.
                {
                    var sun = Glowing(On(mat, "Arena_Cyber_Emit_Orange_Mat", Color.white, 1f), 1.6f);
                    sun.Disc = true;
                    return sun;
                }
                case "eventroom_moon_mat":
                    // A transparent moon picture (AG/Unlit-Transparent) that world UVs would cut into square fragments.
                    return new Look { Key = mat, Skip = true };
                case "wallpaper_dark_mat":
                    return On(mat, "Paint_DarkGrey_Mat", Grey(0.182f), 1f);
                case "metalbarrel_green_mat":
                    // MetalBarrel_Tex is a barrel-side atlas (paint, dark rims, a label): on the barrels' small facets it
                    // speckled them; the painted green, plain.
                    return Mean(mat, new Color(0.14f, 0.52f, 0.38f));
                case "enemy_jumbotron_mat":
                    // The Jumbotron block's atlas (the boss code rebuilds the screen): what is left of it, plain.
                    return Plain(mat, new Color(0.304f, 0.251f, 0.262f));
                case "wood_planks_mat":
                    // Brown planks (~0.45 m boards): the install's plank floor at Rec Room's mean colour.
                    return On(mat, Planks, new Color(0.61f, 0.41f, 0.18f), 2.25f);
                case "crane_red_mat":
                    // An orange crane atlas: its mean on the paint.
                    return On(mat, "Paint_RedOrange_Mat", new Color(0.736f, 0.354f, 0.266f), 2f);
                case "paint_red_mat":
                    return On(mat, "Paint_RedOrange_Mat", new Color(0.915f, 0.229f, 0.117f), 0.5f);
                case "wall_scifi_decor_blue_mat":
                    // A grey panel atlas with a faint blue strip (texture alpha 0.004): plain grey, not a cyan glass box.
                    return Plain(mat, Grey(0.458f));
                case "scifi_emit_cyan_mat":
                    return Glowing(On(mat, "Arena_Cyber_Emit_Teal_Mat", new Color(0f, 0.586f, 1f), 1f), 10f);
                case "scifi_emit_blue_mat":
                    return Glowing(On(mat, "Arena_Cyber_Emit_Teal_Mat", new Color(0.189f, 0.704f, 1f), 1f), 4.45f);
                case "wallsign_red_mat":
                    // Exit signs: WallSign_Tex is level-only; power x the letters' alpha-weighted mean (0.101).
                    return Glowing(On(mat, "Arena_Cyber_Emit_Orange_Mat", new Color(1f, 0.074f, 0.074f), 1f), 0.9f);
                case "wallsign_blue_mat":
                    return Glowing(On(mat, "Arena_Cyber_Emit_Orange_Mat", new Color(0.287f, 0.616f, 1f), 1f), 0.17f);
                case "terminal_scifi_prop_mat":
                    // The install's own sci-fi control-panel texture (gauges, buttons) with its screen emission.
                    return On(mat, "ControlPanel_Mat", new Color(0.59f, 0.6f, 0.59f), 1f);
                case "banner_gym_shield_mat":
                    return Mean(mat, new Color(0.727f, 0.453f, 0.304f));
                case "couch_plush_purple_mat":
                    return Mean(mat, new Color(0.427f, 0.293f, 0.551f));
                case "concrete_mutedblue_50x_mat":
                case "charades_blue_mat":
                case "charades_green_mat":
                {
                    var k = On(mat, Concrete, Opaque(col), 0.02f);
                    k.Key = mat + "|" + ColorUtility.ToHtmlStringRGB(col);
                    return k;
                }

                // ---- level-only meshes (dress boxes, or plates when gtscene re-measures them; jt_materials 5 dress table)
                case "wall_scifi_cover_modular_mat":
                    // The cover walls (base alpha 0.354 is not transparency): on a box, the door picture is the same kind
                    // of grey panel with hazard stripes; as plates the atlas would be noise -> Rec Room's mean grey.
                    return dress ? Glowing(On(mat, Door, Color.white, 1.5f), 0f) : Plain(mat, Grey(0.463f));
                case "shipping_container_instanced_mat":
                    return On(mat, "Metal_StepTrim_Mat", new Color(0.976f, 0.929f, 0.874f), 1f, 2f);
                case "scifi_coverwall01_mat":
                case "scifi_coverwall02_mat":
                case "scifi_coverwall03_mat":
                    // Blue-grey panels with a cyan logo strip (level-only atlases): the mean colour, a soft teal glow.
                    return Glowing(Mean(mat, new Color(0.36f, 0.5f, 0.56f)), 0.25f);
                case "wallvent_mat":
                    return Use(mat, "Vent_Modular_Mat", 1f);
                case "scifi_coversidewall_glass_mat":
                    return Clear(On(mat, GlassTemplate, new Color(0.29f, 0.88f, 1f, 0.3f), 1f));
                case "clock_glass_mat":
                    return Clear(On(mat, GlassTemplate, new Color(0.9f, 0.95f, 1f, 0.15f), 1f));
                case "sandbag_mat":
                    return Mean(mat, new Color(0.485f, 0.446f, 0.367f));
                case "goblet_clock_mat":
                    return Mean(mat, Grey(0.794f));
                case "speaker_intercom_mat":
                    return Mean(mat, new Color(0.713f, 0.65f, 0.56f));
                case "dropship_prop_mat":
                    return Mean(mat, new Color(0.255f, 0.346f, 0.35f));
                case "mirror_quest_mat":
                    return On(mat, "Metal_Grey_Gloss_Mat", new Color(0.3f, 0.32f, 0.35f), 1f);
                case "quest_jumbotron_cauldron_marquee_tex":
                    // The level's marquee art is level-only and the install's nearest (StuntRunner_JumbotronMarquee_Mat) reads
                    // "STUNTRUNNER": a dark board, the lettering is our own neon (RrJumbotronHull.Marquee).
                    return Mean(mat, new Color(0.035f, 0.03f, 0.07f));

                // ---- other level-only materials of levels 38-48: Rec Room's measured mean (NSP iter11mat/effall.tsv)
                case "activitydoor_grey_mat":
                    return Mean(mat, new Color(0.424f, 0.38f, 0.304f));
                case "banner_gym_rect_mat":
                    return Mean(mat, new Color(0.748f, 0.464f, 0.312f));
                case "barrel_scifi_explosive_mat":
                    return Mean(mat, new Color(0.597f, 0.28f, 0.239f));
                case "book_blue_mat":
                    return Mean(mat, new Color(0.363f, 0.327f, 0.294f));
                case "book_red_mat":
                    return Mean(mat, new Color(0.586f, 0.372f, 0.188f));
                case "box_cardboard_discs_mat":
                    return Mean(mat, new Color(0.726f, 0.447f, 0.132f));
                case "chairs_office_green_mat":
                    return Mean(mat, new Color(0.012f, 0.37f, 0.299f));
                case "coiled_wires_mat":
                    return Mean(mat, Grey(0.679f));
                case "doortrigger_mat":
                    return Mean(mat, new Color(0.516f, 0.288f, 0.042f));
                case "dumpster_scifi_orange_mat":
                    return Mean(mat, new Color(0.76f, 0.355f, 0.124f));
                case "paint_medgrey_mat":
                    return Mean(mat, Grey(0.393f));
                case "pokerchairs_mat":
                    return Mean(mat, new Color(0.655f, 0.637f, 0.618f));
                case "poster_paintball_mat":
                    return Mean(mat, new Color(0.597f, 0.501f, 0.432f));
                case "poster_reload_scifi_mat":
                    return Mean(mat, new Color(0.751f, 0.71f, 0.688f));
                case "poster_stayincover_scifi_mat":
                    return Mean(mat, new Color(0.776f, 0.747f, 0.712f));
                case "propbox_scifi_mat":
                case "propbox_mat":
                    return Plain(mat, new Color(0.298f, 0.3f, 0.302f));
                case "propbox_stuntrunner_temp_mat":
                    return Plain(mat, new Color(0.351f, 0.33f, 0.353f));
                case "quest_poster_mat":
                    return Mean(mat, new Color(0.52f, 0.406f, 0.263f));
                case "quest_poster_case_mat":
                    return Mean(mat, new Color(0.543f, 0.485f, 0.432f));
                case "wallpaper_rockwall_mat":
                    return Mean(mat, Grey(0.505f));
                case "unlit_yellow_mat":
                    return Glowing(On(mat, "Arena_Cyber_Emit_Orange_Mat", new Color(1f, 0.858f, 0f), 1f), 2f);
                // Install prop ATLASES (doors, crates, books, chairs, computers, cones...): on level-only meshes the
                // converter re-measures, world UVs would scatter the atlas; Rec Room's measured mean instead.
                case "activitydoor_mat":
                    return Plain(mat, new Color(0.414f, 0.413f, 0.402f));
                case "activtydoor_orange_mat":
                    return Plain(mat, new Color(0.495f, 0.39f, 0.289f));
                case "barrel_table_crate_mat":
                    return Plain(mat, new Color(0.368f, 0.246f, 0.114f));
                case "book_brown_mat":
                    return Plain(mat, new Color(0.504f, 0.394f, 0.176f));
                case "book_green_mat":
                    return Plain(mat, new Color(0.348f, 0.468f, 0.288f));
                case "bookshelf_mat":
                    return Plain(mat, new Color(0.436f, 0.327f, 0.245f));
                case "chair_stackable_wood_mat":
                    return Plain(mat, new Color(0.292f, 0.181f, 0.098f));
                case "computer_retro_mat":
                    return Plain(mat, new Color(0.638f, 0.615f, 0.55f));
                case "doorsign_mat":
                    return Plain(mat, new Color(0.458f, 0.413f, 0.366f));
                case "exitsign_mat":
                    return Plain(mat, new Color(0.621f, 0.52f, 0.519f));
                case "lanternpost_mat":
                    return Plain(mat, new Color(0.335f, 0.246f, 0.167f));
                case "metalbarrel_blue_mat":
                    return Plain(mat, new Color(0.124f, 0.466f, 0.645f));
                case "paintball_target_mat":
                    return Plain(mat, Grey(0.615f));
                case "pingpongtable_mat":
                    return Plain(mat, new Color(0.403f, 0.606f, 0.447f));
                case "pipes_mat":
                    return Plain(mat, new Color(0.355f, 0.397f, 0.405f));
                case "pot_terracotta_mat":
                    return Plain(mat, new Color(0.549f, 0.507f, 0.51f));
                case "projector_mat":
                    return Plain(mat, new Color(0.634f, 0.599f, 0.57f));
                case "speaker_mat":
                    return Plain(mat, Grey(0.155f));
                case "trafficcone_mat":
                    return Plain(mat, new Color(0.467f, 0.277f, 0.199f));
                case "waterbottle_mat":
                    return Plain(mat, new Color(0.44f, 0.549f, 0.638f));
                case "water_jug_orange_mat":
                    return Plain(mat, new Color(0.908f, 0.573f, 0.423f));
                case "paintball_tractortire_mat":
                    return Plain(mat, Grey(0.232f));
                case "poster_stayhydrated_mat":
                    return Fitted(As(mat, 1f));
                case "glowstick_cyan_mat":
                case "glowstick_orange_mat":
                case "glowstick_pink_mat":
                case "glowstick_purple_mat":
                case "glowstick_red_mat":
                {
                    // The hanging / dropped glow sticks: their texture is a stick atlas glowing in its alpha (world UVs would
                    // light random sticks only): the plain emissive template in the level's colour, the whole stick lit.
                    var k = Glowing(On(mat, "Arena_Cyber_Emit_Teal_Mat", Opaque(col), 1f), 3f);
                    k.Key = mat + "|" + ColorUtility.ToHtmlStringRGB(col);
                    return k;
                }

                // ---- decals, cutouts and glow cards (level-only textures): not drawn
                case "garagemarkings_mat":
                case "lockicon_mat":
                case "wires_mat":
                case "pingpongtable_net_mat":
                    return new Look { Key = mat, Skip = true };
            }

            if (l.StartsWith("stagelight_glow", StringComparison.Ordinal))
            {
                return new Look { Key = mat, Skip = true };
            }

            if (l.StartsWith("brick_painted", StringComparison.Ordinal))
            {
                // Brick_Painted_BloodRed / DarkGrey / DarkRed / Green / LightBlue / MedGrey: Brick_Tex x their colour.
                var k = Brick(On(mat, Concrete, Opaque(col), 2f));
                k.Key = mat + "|" + ColorUtility.ToHtmlStringRGB(col);
                return k;
            }

            // ---- defaults
            float tu = TileOf(scale.x), tv = TileOf(scale.y);
            if (inst)
            {
                // An install material not in the table: as it is, at the level's own tiling on metre UVs.
                return As(mat, tu, tv);
            }

            return Default(mat, col, tu, tv);
        }

        /// <summary>A level-only material not in the table (or an install one the install lacks): the closest kind by name.</summary>
        public static Look Default(string mat, Color col, float tileU, float tileV)
        {
            string l = (mat ?? string.Empty).ToLowerInvariant();
            bool white = col.r > 0.97f && col.g > 0.97f && col.b > 0.97f;
            Look k;
            if (l.Contains("glass") || l.Contains("fence"))
            {
                // Real glass (and chain-link fences, which Rec Room draws see-through): the only transparent surfaces.
                k = Clear(On(mat, GlassTemplate, new Color(col.r, col.g, col.b, Mathf.Clamp(col.a, 0.12f, 0.4f)), 1f));
            }
            else if (l.Contains("emit") || l.Contains("glow"))
            {
                k = On(mat, "Arena_Cyber_Emit_Teal_Mat", Opaque(col), 1f);
            }
            else if (l.Contains("rock") || l.Contains("boulder") || l.Contains("cliff"))
            {
                k = On(mat, "Rocks_Tiling_Background_Mat", white ? Color.white : Opaque(col), 3f);
            }
            else if (l.StartsWith("concrete", StringComparison.Ordinal))
            {
                k = On(mat, Concrete, Opaque(col), tileU, tileV);
            }
            else if (l.StartsWith("metal", StringComparison.Ordinal))
            {
                k = On(mat, Metal, Opaque(col), tileU, tileV);
            }
            else if (l.StartsWith("wood", StringComparison.Ordinal))
            {
                k = On(mat, "Wood_Horizontal_Orange_Mat", white ? Color.white : Opaque(col), 2f);
            }
            else
            {
                // A plain paint colour on the grid-lit paint template (white or unknown -> a quiet mid grey).
                k = On(mat, Pen, white ? Grey(0.6f) : Opaque(col), 1f);
            }

            k.Key = mat + "|" + ColorUtility.ToHtmlStringRGBA(col);
            return k;
        }

        /// <summary>
        /// Iteration 16: a material of Rec Room's own July 2025 Rec Center, measured by DevNotes/tools/rcscene from the
        /// cached Studio scene (the Rec Center's data build, Data/RecCenter_Final.bytes). Its platemat carries what the
        /// Studio material shows: col = _BaseColor, mean = its base texture's mean colour (alpha-weighted for cut-outs),
        /// glow = _EmissionColor x the emission map's mean, clear = transparent. The surface is Rec Room's measured mean
        /// albedo (col x mean) on the paint template (<see cref="Mean"/>), with these exceptions: the walls keep the
        /// concrete template under their vertex-colour paint (as <see cref="Plan"/> draws Wall_Vertcolor_mat), the hall
        /// floor is the install's plank floor and the red brick the concrete template with the brick relief, both tinted
        /// to the measured colour; an install material that tiles (scale != 1), an install picture the plates fit, and
        /// the install glass are used as they are; what glows clearly (the marquee, the fluorescent tubes, the meeting
        /// bulbs, glow sticks) is the emissive template in its glow colour.
        /// </summary>
        public static Look PlanMeasured(string mat, Color col, Vector2 scale, bool inst, Color? mean, Color? glow, bool clear)
        {
            string l = (mat ?? string.Empty).ToLowerInvariant();
            var m = mean ?? Color.white;
            var albedo = new Color(Mathf.Clamp01(col.r * m.r), Mathf.Clamp01(col.g * m.g), Mathf.Clamp01(col.b * m.b), 1f);
            switch (l)
            {
                case "wall_vertcolor_mat":
                    return Plan(mat, col, scale, inst, false);
                case "recenter_subwaytile_vertcolor_mat":
                    // Brick_DarkGrout_tex x30 (level-only): the concrete template's grain at the tiles' mean, under the vertex paint.
                    return On(mat, Concrete, Grey(Mathf.Clamp01(m.g / 0.735f)), 0.25f);
                case "wood_floor_mat":
                    // Wood FloorTiled_tex (level-only, light planks) x the orange-brown base: the install's plank floor at that colour.
                    return On(mat, Planks, Lift(albedo, 1.25f), 2f);
                case "recenter_brick_red":
                case "fence_brick_mat":
                    // Brick_Grout_tex / Fence_Brick_tex are level-only: the concrete template (mean 0.735) tinted to the
                    // bricks' mean, with the install's brick relief.
                    return Brick(On(mat, Concrete, Lift(albedo, 1f / 0.735f), 1.5f));
                case "scifi_rug_mat":
                case "dormroom_bulletinboard_mat":
                case "codeofconduct_mat":
                    // Install pictures: fitted to each plate.
                    if (inst)
                    {
                        return Fitted(As(mat, 1f));
                    }

                    break;
            }

            if (clear)
            {
                return inst ? As(mat, TileOf(scale.x), TileOf(scale.y)) : Clear(On(mat, GlassTemplate, new Color(col.r, col.g, col.b, Mathf.Clamp(col.a, 0.12f, 0.4f)), 1f));
            }

            if (glow.HasValue)
            {
                var g = glow.Value;
                float p = Mathf.Max(g.r, Mathf.Max(g.g, g.b));
                float a = Mathf.Max(albedo.r, Mathf.Max(albedo.g, albedo.b));
                if (p > 0.3f || p > 0.5f * a)
                {
                    var k = Glowing(On(mat, "Arena_Cyber_Emit_Orange_Mat", new Color(g.r / p, g.g / p, g.b / p, 1f), 1f), Mathf.Clamp(p * 2f, 0.3f, 4f));
                    k.Key = mat + "|glow";
                    return k;
                }
            }

            if (inst && (Mathf.Abs(scale.x - 1f) > 0.01f || Mathf.Abs(scale.y - 1f) > 0.01f))
            {
                // An install material that tiles (wood grain, concrete): as it is.
                return Plan(mat, col, scale, inst, false);
            }

            // Atlases and level-only textures: Rec Room's measured mean (a plain surface reads better than a wrong pattern).
            return Mean(mat, albedo);
        }

        private static Color Lift(Color c, float f)
        {
            return new Color(Mathf.Clamp01(c.r * f), Mathf.Clamp01(c.g * f), Mathf.Clamp01(c.b * f), 1f);
        }

        /// <summary>The last resort when even a template is missing: the paint template in the look's colour.</summary>
        public static Look PenOf(Look look, Color col)
        {
            var c = look != null && look.SetTint ? look.Tint : col;
            var k = On(look != null ? look.Key : "?", Pen, Opaque(c), look != null ? look.TileU : 1f, look != null ? look.TileV : 0f);
            k.Key = (look != null ? look.Key : "?") + "|pen";
            k.Glass = false;
            if (look != null)
            {
                k.Fit = look.Fit;
                k.Rotate = look.Rotate;
                k.Disc = look.Disc;
            }

            return k;
        }

        /// <summary>Whether the material's shader multiplies the mesh vertex colour in (install disassembly, jt_materials 2).</summary>
        public static bool ReadsVertexColour(Material m)
        {
            if (m == null || m.shader == null)
            {
                return false;
            }

            // Checked: AG/Standard-Opaque (agstd) and AG/Standard-Opaque-Emission* read COLOR (NSP/shdis). Instanced,
            // Decal, Transparent and the maker pen are not verified: their plates get per-colour clones instead.
            string n = m.shader.name ?? string.Empty;
            return n == "AG/Standard-Opaque" || n.StartsWith("AG/Standard-Opaque-Emission", StringComparison.Ordinal);
        }

        /// <summary>
        /// Whether a material would draw see-through (blended, additive or alpha-clipped): by its shader name (AG's
        /// transparent / additive / cutout variants, particles, fog), its URP surface keywords and floats. The hull's
        /// surfaces must not (only the real glass may); queue 2501 alone is not a sign - RrMaterials moves opaque shaders
        /// without a DepthNormals pass there.
        /// </summary>
        public static bool SeeThrough(Material m)
        {
            if (m == null || m.shader == null)
            {
                return false;
            }

            string n = m.shader.name ?? string.Empty;
            if (Has(n, "Transparent") || Has(n, "Additive") || Has(n, "Particles") || Has(n, "Fog") || Has(n, "Fade") || Has(n, "Cutout") || Has(n, "AlphaClip")
                || Has(n, "AlphaTest") || Has(n, "DoNotRender") || Has(n, "Mirror"))
            {
                return true;
            }

            if (m.IsKeywordEnabled("_ALPHATEST_ON") || m.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT") || m.IsKeywordEnabled("_ALPHABLEND_ON") || m.IsKeywordEnabled("_ALPHAPREMULTIPLY_ON"))
            {
                return true;
            }

            return (m.HasProperty("_Surface") && m.GetFloat("_Surface") > 0.5f) || (m.HasProperty("_AlphaClip") && m.GetFloat("_AlphaClip") > 0.5f);
        }

        /// <summary>
        /// Whether a see-through material is only alpha-CLIPPED on an opaque shader (it still writes depth and blends
        /// nothing): a clone with _Cutoff 0 and clipping off draws it solid. Blended / additive shaders are not.
        /// </summary>
        public static bool ClipOnly(Material m)
        {
            if (m == null || m.shader == null)
            {
                return false;
            }

            string n = m.shader.name ?? string.Empty;
            if (Has(n, "Transparent") || Has(n, "Additive") || Has(n, "Particles") || Has(n, "Fog") || Has(n, "Fade") || Has(n, "DoNotRender") || Has(n, "Mirror"))
            {
                return false;
            }

            if (m.IsKeywordEnabled("_SURFACE_TYPE_TRANSPARENT") || m.IsKeywordEnabled("_ALPHABLEND_ON") || m.IsKeywordEnabled("_ALPHAPREMULTIPLY_ON") || (m.HasProperty("_Surface") && m.GetFloat("_Surface") > 0.5f))
            {
                return false;
            }

            return true;
        }

        /// <summary>Makes an alpha-clipped opaque clone solid (see <see cref="ClipOnly"/>).</summary>
        public static void Unclip(Material m)
        {
            if (m == null)
            {
                return;
            }

            m.DisableKeyword("_ALPHATEST_ON");
            if (m.HasProperty("_AlphaClip"))
            {
                m.SetFloat("_AlphaClip", 0f);
            }

            if (m.HasProperty("_Cutoff"))
            {
                m.SetFloat("_Cutoff", 0f);
            }

            if (m.renderQueue >= 2450 && m.renderQueue < 2500)
            {
                m.renderQueue = 2000;
            }
        }

        // ------------------------------------------------------------------ helpers

        private static bool Has(string s, string part)
        {
            return s.IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static Look As(string mat, float tileU, float tileV = 0f)
        {
            return new Look { Key = mat, Template = mat, AsIs = true, TileU = tileU, TileV = tileV };
        }

        private static Look Use(string mat, string template, float tileU, float tileV = 0f)
        {
            return new Look { Key = mat, Template = template, TileU = tileU, TileV = tileV };
        }

        private static Look On(string mat, string template, Color tint, float tileU, float tileV = 0f)
        {
            return new Look { Key = mat, Template = template, Tint = tint, SetTint = true, TileU = tileU, TileV = tileV };
        }

        /// <summary>The cinderblock / painted brick: the concrete template (its _BaseMap is Concrete_Tex) + the level's brick relief.</summary>
        private static Look Brick(Look k)
        {
            k.Bump = "Brick_Norm";
            k.Spec = "Brick_Spec";
            return k;
        }

        private static Look Glowing(Look k, float power)
        {
            k.Glow = power;
            return k;
        }

        private static Look Clear(Look k)
        {
            k.Glass = true;
            return k;
        }

        private static Look Fitted(Look k)
        {
            k.Fit = true;
            return k;
        }

        private static Look Turned(Look k)
        {
            k.Rotate = true;
            return k;
        }

        /// <summary>A plain paint colour that reproduces Rec Room's measured mean albedo on the paint template's texture.</summary>
        private static Look Mean(string mat, Color mean)
        {
            var lin = mean.linear;
            var t = new Color(Mathf.Clamp01(lin.r / PaintTexLinear), Mathf.Clamp01(lin.g / PaintTexLinear), Mathf.Clamp01(lin.b / PaintTexLinear), 1f).gamma;
            return On(mat, Pen, t, 1f);
        }

        /// <summary>
        /// A plain surface at Rec Room's measured mean albedo for a level material whose texture is an atlas: the dull
        /// metal (its fine grain and gloss) when a tint of at most 1 reaches the mean, else the paint. No glow (the
        /// templates' texture alpha is 1: a glow would light the whole face, not a strip).
        /// </summary>
        private static Look Plain(string mat, Color mean)
        {
            var lin = mean.linear;
            float r = lin.r / MetalTexLinear, g = lin.g / MetalTexLinear, b = lin.b / MetalTexLinear;
            if (Mathf.Max(r, Mathf.Max(g, b)) > 1.02f)
            {
                return Glowing(Mean(mat, mean), 0f);
            }

            var t = new Color(Mathf.Clamp01(r), Mathf.Clamp01(g), Mathf.Clamp01(b), 1f).gamma;
            return Glowing(On(mat, Metal, t, 1f), 0f);
        }

        private static Color Grey(float v)
        {
            return new Color(v, v, v, 1f);
        }

        private static Color Opaque(Color c)
        {
            return new Color(c.r, c.g, c.b, 1f);
        }

        /// <summary>Metres per repeat for a level scale on metre UVs (UV density 1, the ProBuilder faces').</summary>
        private static float TileOf(float scale)
        {
            return scale > 1e-4f ? Mathf.Clamp(1f / scale, 0.01f, 50f) : 1f;
        }
    }
}
