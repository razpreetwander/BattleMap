using UnityEngine;

namespace BattleMaps.CharacterOutfits
{
    /// <summary>
    /// Reconstructed from BattleMaps.CharacterOutfits.SkinOutfitType & CharacterOutfitsController (29 methods).
    /// Procedurally constructs stylized 3D hats, visors, horns, halos, and accessories on player avatars!
    /// </summary>
    public enum SkinOutfitType
    {
        ClassicCube = 0,
        KnightHelmet = 1,
        WizardHat = 2,
        AstronautVisor = 3,
        RoyalCrown = 4,
        CyberNeon = 5,
        NinjaBand = 6,
        VikingHorns = 7,
        FlameHalo = 8,
        IceCrystal = 9
    }

    public static class CharacterOutfitBuilder
    {
        public static readonly string[] OutfitNames =
        {
            "Classic Cube", "Knight Helmet", "Wizard Hat", "Astronaut Visor", "Royal Crown",
            "Cyber Neon", "Ninja Headband", "Viking Horns", "Flame Halo", "Ice Crystal"
        };

        public static void ApplyOutfit(Transform avatarRoot, SkinOutfitType outfit, Color primaryColor)
        {
            if (avatarRoot == null) return;

            var old = avatarRoot.Find("OutfitAttachment");
            if (old != null) Object.Destroy(old.gameObject);

            if (outfit == SkinOutfitType.ClassicCube) return;

            var holder = new GameObject("OutfitAttachment").transform;
            holder.SetParent(avatarRoot, false);
            Shader shader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Unlit/Color");

            switch (outfit)
            {
                case SkinOutfitType.KnightHelmet:
                    AddPart(holder, PrimitiveType.Cube, new Vector3(0f, 0.38f, 0f), new Vector3(1.04f, 0.25f, 1.04f), new Color(0.72f, 0.76f, 0.82f), shader);
                    AddPart(holder, PrimitiveType.Cube, new Vector3(0f, 0.58f, 0f), new Vector3(0.18f, 0.35f, 0.90f), new Color(0.90f, 0.18f, 0.22f), shader);
                    break;

                case SkinOutfitType.WizardHat:
                    AddPart(holder, PrimitiveType.Cylinder, new Vector3(0f, 0.42f, 0f), new Vector3(1.25f, 0.05f, 1.25f), new Color(0.28f, 0.16f, 0.62f), shader);
                    AddPart(holder, PrimitiveType.Capsule, new Vector3(0f, 0.72f, -0.05f), new Vector3(0.55f, 0.45f, 0.55f), new Color(0.36f, 0.20f, 0.78f), shader);
                    break;

                case SkinOutfitType.AstronautVisor:
                    AddPart(holder, PrimitiveType.Sphere, new Vector3(0f, 0.16f, 0.22f), new Vector3(0.96f, 0.65f, 0.72f), new Color(0.16f, 0.82f, 1.0f), shader);
                    AddPart(holder, PrimitiveType.Cube, new Vector3(0f, 0.08f, -0.52f), new Vector3(0.65f, 0.55f, 0.32f), new Color(0.92f, 0.94f, 0.98f), shader);
                    break;

                case SkinOutfitType.RoyalCrown:
                    AddPart(holder, PrimitiveType.Cylinder, new Vector3(0f, 0.48f, 0f), new Vector3(0.72f, 0.18f, 0.72f), new Color(1.0f, 0.82f, 0.14f), shader);
                    AddPart(holder, PrimitiveType.Sphere, new Vector3(0f, 0.66f, 0f), Vector3.one * 0.22f, new Color(0.95f, 0.20f, 0.30f), shader);
                    break;

                case SkinOutfitType.CyberNeon:
                    AddPart(holder, PrimitiveType.Cube, new Vector3(0f, 0.20f, 0.48f), new Vector3(0.98f, 0.22f, 0.14f), new Color(0.05f, 0.98f, 0.88f), shader);
                    AddPart(holder, PrimitiveType.Cube, new Vector3(0f, 0.42f, 0f), new Vector3(1.06f, 0.08f, 1.06f), new Color(0.96f, 0.18f, 0.76f), shader);
                    break;

                case SkinOutfitType.NinjaBand:
                    AddPart(holder, PrimitiveType.Cube, new Vector3(0f, 0.24f, 0f), new Vector3(1.04f, 0.18f, 1.04f), new Color(0.88f, 0.14f, 0.18f), shader);
                    AddPart(holder, PrimitiveType.Cube, new Vector3(0.24f, 0.22f, -0.68f), new Vector3(0.14f, 0.14f, 0.55f), new Color(0.88f, 0.14f, 0.18f), shader);
                    break;

                case SkinOutfitType.VikingHorns:
                    AddPart(holder, PrimitiveType.Cylinder, new Vector3(0f, 0.42f, 0f), new Vector3(0.92f, 0.16f, 0.92f), new Color(0.42f, 0.36f, 0.32f), shader);
                    var lHorn = AddPart(holder, PrimitiveType.Capsule, new Vector3(-0.56f, 0.56f, 0f), new Vector3(0.18f, 0.32f, 0.18f), new Color(0.95f, 0.92f, 0.82f), shader);
                    lHorn.localRotation = Quaternion.Euler(0f, 0f, 40f);
                    var rHorn = AddPart(holder, PrimitiveType.Capsule, new Vector3(0.56f, 0.56f, 0f), new Vector3(0.18f, 0.32f, 0.18f), new Color(0.95f, 0.92f, 0.82f), shader);
                    rHorn.localRotation = Quaternion.Euler(0f, 0f, -40f);
                    break;

                case SkinOutfitType.FlameHalo:
                    AddPart(holder, PrimitiveType.Cylinder, new Vector3(0f, 0.72f, 0f), new Vector3(0.82f, 0.06f, 0.82f), new Color(1.0f, 0.45f, 0.08f), shader);
                    AddPart(holder, PrimitiveType.Sphere, new Vector3(0f, 0.78f, 0f), Vector3.one * 0.26f, new Color(1.0f, 0.88f, 0.22f), shader);
                    break;

                case SkinOutfitType.IceCrystal:
                    var spire = AddPart(holder, PrimitiveType.Cube, new Vector3(0f, 0.65f, 0f), new Vector3(0.42f, 0.55f, 0.42f), new Color(0.45f, 0.92f, 1.0f), shader);
                    spire.localRotation = Quaternion.Euler(0f, 45f, 0f);
                    break;
            }
        }

        private static Transform AddPart(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Color col, Shader shader)
        {
            var go = GameObject.CreatePrimitive(type);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            Object.Destroy(go.GetComponent<Collider>());
            go.GetComponent<MeshRenderer>().sharedMaterial = new Material(shader) { color = col };
            return go.transform;
        }
    }
}
