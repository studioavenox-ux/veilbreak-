using System.Collections.Generic;
using UnityEngine;

namespace Veilbreak
{
    // Hand-composed procedural prototype art. No rigs, imported models, textures or new gameplay controllers.
    public sealed class PrototypeSceneArt
    {
        readonly PrimitiveFactory art;
        readonly Material stone, trim, dark, plate, leather, bronze, joint, skin, hair, leaf, soil, cyan, violet;
        public Material Energy { get; }
        public Material Warning { get; }
        public PrototypeSceneArt(PrimitiveFactory factory)
        {
            art = factory;
            stone = art.Material("Courtyard / weathered sandstone", new Color(.36f,.34f,.29f));
            trim = art.Material("Courtyard / worn limestone edges", new Color(.57f,.53f,.43f));
            dark = art.Material("Warrior / charcoal cloth", new Color(.065f,.085f,.11f));
            plate = art.Material("Warrior / blue charcoal plates", new Color(.19f,.24f,.29f));
            leather = art.Material("Warrior / umber straps", new Color(.24f,.13f,.075f));
            bronze = art.Material("Guardian / aged bronze armor", new Color(.38f,.31f,.21f));
            plate.SetFloat("_Metallic", .35f); bronze.SetFloat("_Metallic", .55f);
            joint = art.Material("Guardian / dark iron joints", new Color(.095f,.12f,.14f));
            skin = art.Material("Warrior / face", new Color(.68f,.44f,.31f));
            hair = art.Material("Warrior / dark hair", new Color(.025f,.03f,.04f));
            leaf = art.Material("Living / sage foliage", new Color(.22f,.32f,.12f));
            soil = art.Material("Living / soil", new Color(.16f,.135f,.10f));
            cyan = art.Material("Warrior and Living / cyan energy", new Color(.04f,.65f,.78f), true);
            violet = art.Material("Broken / violet crystal", new Color(.4f,.075f,.68f), true);
            Energy = art.Material("Guardian / state energy", new Color(.04f,.65f,.78f), true);
            Warning = art.Material("Combat / danger footprint", new Color(.9f,.23f,.04f), true);
        }
        Transform Part(string name, Transform parent, Vector3 pos, Vector3 size, Material mat, Vector3 rotation = default)
        { var t = art.Facet(name, parent, pos, size, mat); t.localRotation = Quaternion.Euler(rotation); return t; }
        Transform Box(string name, Transform parent, Vector3 pos, Vector3 size, Material mat, bool solid = false)
            => art.Shape(name, parent, PrimitiveType.Cube, pos, size, mat, solid);
        Transform Shard(string name, Transform parent, Vector3 pos, Vector3 size, Material mat, Vector3 rotation = default)
        { var t = art.Facet(name, parent, pos, size, mat, true); t.localRotation = Quaternion.Euler(rotation); return t; }

        public Transform Warrior(Transform root, out Transform blade)
        {
            // +Z faces into the arena; shoulders, scarf and back plates read from the follow camera behind -Z.
            var v = art.Group("Warrior / assembled low-poly placeholder", root, Vector3.zero);
            Part("Cloth tunic", v, new Vector3(0,1.02f,0), new Vector3(.54f,.65f,.34f), dark);
            Part("Chest cuirass", v, new Vector3(0,1.22f,.10f), new Vector3(.63f,.37f,.25f), plate);
            Part("Back armor", v, new Vector3(0,1.23f,-.16f), new Vector3(.57f,.37f,.16f), plate);
            Part("Lower overlapping cuirass", v, new Vector3(0,1.02f,.17f), new Vector3(.5f,.17f,.14f), plate);
            Box("Belt", v, new Vector3(0,.85f,0), new Vector3(.59f,.12f,.39f), leather);
            Box("Belt clasp", v, new Vector3(0,.85f,.22f), new Vector3(.13f,.11f,.06f), bronze);
            Part("Back diagonal strap", v, new Vector3(0,1.17f,-.27f), new Vector3(.08f,.56f,.04f), leather, new Vector3(0,0,-25));
            Part("Neck scarf", v, new Vector3(0,1.48f,-.035f), new Vector3(.48f,.16f,.38f), dark);
            Part("Trailing scarf", v, new Vector3(-.16f,1.13f,-.31f), new Vector3(.26f,.58f,.07f), dark, new Vector3(-12,0,-10));
            Part("Head", v, new Vector3(0,1.67f,.015f), new Vector3(.34f,.39f,.32f), skin);
            Part("Hair cap", v, new Vector3(0,1.84f,-.045f), new Vector3(.39f,.15f,.36f), hair);
            for (int i=0;i<5;i++)
                Shard("Swept hair lock", v, new Vector3(-.17f+i*.075f,1.91f,-.045f), new Vector3(.12f,.24f,.19f), hair, new Vector3(-25,0,-30+i*8));
            Part("Front fringe", v, new Vector3(-.09f,1.78f,.17f), new Vector3(.13f,.22f,.08f), hair, new Vector3(0,0,-24));
            for (int side=-1;side<=1;side+=2)
            {
                float x=side;
                Part("Upper arm", v, new Vector3(x*.37f,1.22f,0), new Vector3(.19f,.32f,.22f), dark, new Vector3(0,0,x*12));
                Part("Layered shoulder guard", v, new Vector3(x*.39f,1.43f,0), new Vector3(.32f,.20f,.39f), plate, new Vector3(0,0,x*-15));
                Part("Forearm bracer", v, new Vector3(x*.45f,.98f,.04f), new Vector3(.20f,.30f,.23f), plate);
                Part("Glove", v, new Vector3(x*.46f,.80f,.09f), new Vector3(.18f,.17f,.20f), dark);
                Part("Split coat panel", v, new Vector3(x*.22f,.64f,-.11f), new Vector3(.25f,.44f,.12f), dark, new Vector3(-8,0,x*-8));
                Part("Thigh", v, new Vector3(x*.17f,.61f,0), new Vector3(.24f,.38f,.28f), dark);
                Part("Knee plate", v, new Vector3(x*.17f,.41f,.12f), new Vector3(.23f,.20f,.15f), plate);
                Part("Shin guard", v, new Vector3(x*.17f,.25f,.04f), new Vector3(.22f,.31f,.25f), plate);
                Part("Boot", v, new Vector3(x*.17f,.09f,.09f), new Vector3(.27f,.18f,.42f), dark);
            }
            Part("Shoulder core housing", v, new Vector3(-.45f,1.42f,-.12f), new Vector3(.26f,.24f,.20f), bronze);
            Shard("Cyan shoulder core", v, new Vector3(-.46f,1.44f,-.235f), new Vector3(.15f,.18f,.08f), cyan);
            // Keep the existing combat swing pivot; only its child geometry changes.
            blade = art.Group("Blade swing", v, new Vector3(.46f,.82f,.12f));
            Part("Sword grip", blade, new Vector3(0,0,.13f), new Vector3(.10f,.11f,.32f), leather);
            Part("Sword crossguard", blade, new Vector3(0,0,.30f), new Vector3(.38f,.12f,.12f), plate);
            Part("Blade dark spine", blade, new Vector3(0,0,.87f), new Vector3(.19f,.10f,1.10f), plate);
            var edge = Shard("Visible cyan energy blade", blade, new Vector3(0,.015f,.94f), new Vector3(.14f,1.35f,.065f), cyan);
            edge.localRotation = Quaternion.Euler(90,0,0);
            return v;
        }

        public Transform Guardian(Transform root, out Transform core, out Transform armor)
        {
            var v = art.Group("Guardian / assembled mechanical placeholder", root, Vector3.zero);
            Part("Inner chassis", v, new Vector3(0,2.9f,0), new Vector3(2.35f,1.85f,1.15f), joint);
            Part("Broad dorsal armor", v, new Vector3(0,3.4f,-.39f), new Vector3(2.8f,1.03f,.65f), bronze);
            Part("Waist piston block", v, new Vector3(0,1.98f,0), new Vector3(1.55f,.45f,1.0f), joint);
            for(int side=-1;side<=1;side+=2)
            {
                float x=side;
                Part("Sloping breastplate", v, new Vector3(x*.85f,3.25f,.60f), new Vector3(.95f,1.25f,.42f), bronze, new Vector3(0,0,x*-12));
                Part("Hip shell", v, new Vector3(x*.73f,1.91f,.12f), new Vector3(.83f,.62f,1.05f), bronze);
                Part("Upper leg", v, new Vector3(x*.73f,1.47f,0), new Vector3(.65f,.72f,.70f), joint);
                Part("Knee joint", v, new Vector3(x*.76f,1.04f,.20f), new Vector3(.72f,.42f,.75f), joint);
                Part("Knee shield", v, new Vector3(x*.76f,1.10f,.51f), new Vector3(.76f,.55f,.30f), bronze);
                Part("Shin shell", v, new Vector3(x*.78f,.61f,.05f), new Vector3(.74f,.83f,.75f), bronze);
                Part("Heavy foot", v, new Vector3(x*.78f,.19f,.28f), new Vector3(.95f,.38f,1.35f), joint);
                Part("Shoulder socket", v, new Vector3(x*1.42f,3.48f,0), new Vector3(.85f,.8f,.9f), joint);
                Part("Pauldron lower tier", v, new Vector3(x*1.64f,3.65f,0), new Vector3(1.18f,.49f,1.25f), bronze, new Vector3(0,0,x*-12));
                Part("Pauldron upper tier", v, new Vector3(x*1.52f,3.96f,-.06f), new Vector3(1.17f,.33f,1.10f), bronze, new Vector3(0,0,x*-18));
                Part("Upper arm piston", v, new Vector3(x*1.85f,3.05f,0), new Vector3(.57f,.77f,.65f), joint);
                Part("Elbow", v, new Vector3(x*1.93f,2.62f,.06f), new Vector3(.69f,.39f,.73f), joint);
                Part("Forearm gauntlet", v, new Vector3(x*1.96f,2.22f,.18f), new Vector3(.85f,.85f,.93f), bronze);
                Part("Fist", v, new Vector3(x*1.97f,1.67f,.22f), new Vector3(.83f,.52f,.92f), joint);
                Part("Shoulder energy vent", v, new Vector3(x*1.65f,3.72f,-.66f), new Vector3(.48f,.10f,.05f), Energy);
                Part("Shin energy seam", v, new Vector3(x*.78f,.62f,.44f), new Vector3(.10f,.47f,.045f), Energy);
            }
            Part("Neck socket", v, new Vector3(0,3.98f,0), new Vector3(.76f,.40f,.73f), joint);
            Part("Head helmet", v, new Vector3(0,4.36f,.12f), new Vector3(.96f,.68f,.85f), bronze);
            Part("Dark face mask", v, new Vector3(0,4.32f,.53f), new Vector3(.69f,.43f,.12f), joint);
            Part("Glowing visor", v, new Vector3(0,4.41f,.61f), new Vector3(.57f,.10f,.045f), Energy);
            // Permanent frame and recessed glow; living shutters are a separate toggled visual group.
            Part("Core surround", v, new Vector3(0,3.0f,.75f), new Vector3(1.38f,1.46f,.37f), joint);
            core = art.Group("Large exposed central core", v, new Vector3(0,3.0f,1.01f));
            Part("Core lens", core, Vector3.zero, new Vector3(1.0f,1.12f,.21f), Energy);
            Shard("Core heart", core, new Vector3(0,0,.15f), new Vector3(.43f,.77f,.16f), Energy);
            armor = art.Group("Living core armor shutters", v, Vector3.zero);
            for(int side=-1;side<=1;side+=2)
                Part("Overlapping core shutter", armor, new Vector3(side*.40f,3.0f,1.25f), new Vector3(.50f,1.19f,.20f), bronze, new Vector3(0,0,side*8));
            return v;
        }

        Transform Reactive(string name, Transform parent, VeilState state, List<VeilReactiveObject> objects)
        {
            var group = art.Group(name, parent, Vector3.zero);
            var reactive = group.gameObject.AddComponent<VeilReactiveObject>(); reactive.ActiveState = state; objects.Add(reactive);
            return group;
        }
        public List<VeilReactiveObject> Courtyard(Transform parent)
        {
            var world = art.Group("Veil Arena / ruined courtyard", parent, Vector3.zero);
            Box("Shared courtyard foundation", world, new Vector3(0,-.5f,0), new Vector3(32,1,32), stone, true);
            // Offset stonework, subtly varied by a deterministic pattern, combined into just two cosmetic meshes.
            // The common floor collider remains smooth and unchanged for movement/shift safety.
            for(int variant=0;variant<2;variant++)
            {
                var tiles = art.Group(variant==0 ? "Weathered tile field" : "Lighter walkway stones", world, Vector3.zero);
                for(int row=0;row<16;row++) for(int col=0;col<16;col++)
                {
                    bool path = col==7 || col==8 || row==7 || row==8;
                    if ((path ? 1:0)!=variant) continue;
                    float x=-14.9f+col*1.96f+(row%2)*.72f;
                    float z=-14.9f+row*1.96f;
                    Box("Individual worn flagstone", tiles, new Vector3(x,-.075f,z), new Vector3(1.9f,.18f,1.9f), variant==0 ? stone:trim);
                }
                art.CombineDecor(tiles, variant==0 ? stone:trim);
            }
            // Collision boundary is continuous; broken coping stones supply the irregular visible silhouette.
            for(int side=0;side<4;side++)
            {
                var wall = art.Group("Courtyard broken parapet", world, Vector3.zero); wall.localRotation=Quaternion.Euler(0,side*90,0);
                Box("Boundary stone", wall, new Vector3(0,1.0f,16), new Vector3(33,2,.8f), stone, true);
                for(int i=0;i<12;i++)
                    Part("Broken coping block", wall, new Vector3(-15+i*2.7f,2.0f+(i%3)*.08f,16), new Vector3(2.2f,.3f+(i%3)*.14f,1.05f), trim, new Vector3(0,(i%3-1)*6,0));
            }
            for(int i=0;i<8;i++)
            {
                float a=(i*45+22.5f)*Mathf.Deg2Rad;
                var column=art.Group("Varied ruined pier",world,new Vector3(Mathf.Sin(a)*13.8f,0,Mathf.Cos(a)*13.8f));
                float h= i%3==0 ? 2.0f : i%3==1 ? 3.6f : 4.7f;
                Box("Pier plinth",column,new Vector3(0,.22f,0),new Vector3(1.7f,.44f,1.7f),trim,true);
                Box("Pier collision",column,new Vector3(0,h*.5f,0),new Vector3(.86f,h,.86f),stone,true);
                for(int j=0;j<Mathf.FloorToInt(h/.65f);j++)
                    Part("Masonry drum",column,new Vector3(0,.64f+j*.65f,0),new Vector3(1.15f,.61f,1.15f),stone,new Vector3(0,j%2*10,0));
                Part("Fractured capital",column,new Vector3(.08f,h+.12f,-.04f),new Vector3(1.5f,.33f,1.45f),trim,new Vector3(0,i*13,5));
            }
            Arch(world,-14,4.7f,false); Arch(world,14,6.0f,true);
            // A western fallen lintel and eastern crystal reliquary are recognizable, grounded landmarks.
            Part("Fallen western lintel",world,new Vector3(-12,.55f,-9),new Vector3(3.6f,.8f,1.1f),trim,new Vector3(0,28,5));
            Box("Reliquary pedestal",world,new Vector3(12,.6f,10),new Vector3(1.5f,1.2f,1.5f),trim,true);
            Shard("Reliquary crystal",world,new Vector3(12,2.0f,10),new Vector3(.55f,1.7f,.55f),Energy);
            var objects=new List<VeilReactiveObject>();
            var living=Reactive("Living / sheltered gardens",world,VeilState.Living,objects);
            var broken=Reactive("Broken / crystal ruptures",world,VeilState.Broken,objects);
            foreach(int side in new[]{-1,1})
            {
                for(int i=0;i<4;i++)
                {
                    Vector3 p=new Vector3(side*(11.8f+(i%2)*1.1f),.05f,-7+i*4.2f);
                    Part("Garden soil pocket",living,p,new Vector3(1.3f,.12f,1.6f),soil);
                    for(int j=0;j<3;j++)
                        Shard("Low sage foliage",living,p+new Vector3((j-1)*.28f,.32f,0),new Vector3(.48f,.58f,.58f),leaf,new Vector3(0,j*53,side*15));
                    for(int j=0;j<3;j++)
                        Shard("Violet fracture crystal",broken,p+new Vector3((j-1)*.29f,.48f+j*.13f,0),new Vector3(.25f,.9f+j*.26f,.27f),violet,new Vector3(0,j*42,side*(j-1)*17));
                    Part("Fracture bed",broken,p,new Vector3(1.35f,.18f,1.5f),joint);
                }
            }
            // Preserve the original gate/platform footprints and low step height; solid pieces are real BoxColliders.
            var livingGate=Reactive("Living / western standing masonry",world,VeilState.Living,objects);
            var brokenGate=Reactive("Broken / eastern fractured masonry",world,VeilState.Broken,objects);
            Box("Living route cover",livingGate,new Vector3(-7,1,1),new Vector3(1.4f,2,5),stone,true);
            Part("Living cover coping",livingGate,new Vector3(-7,2.1f,1),new Vector3(1.6f,.2f,5.1f),trim);
            // Three unequal chunks replace a single debug block, with visible and physical gaps.
            for(int i=0;i<3;i++)
            {
                float h=1.4f+i*.3f;
                Box("Broken route stone chunk",brokenGate,new Vector3(7,h*.5f,-.65f+i*1.65f),new Vector3(1.4f,h,1.5f),stone,true);
                Shard("Ruptured cover seam",brokenGate,new Vector3(7,h+.22f,-.65f+i*1.65f),new Vector3(.25f,.65f,.35f),violet);
            }
            var livingPlatform=Reactive("Living / intact western terrace",world,VeilState.Living,objects);
            Box("Intact stepping terrace",livingPlatform,new Vector3(-10,.2f,-5),new Vector3(4,.4f,4),trim,true);
            var brokenPlatform=Reactive("Broken / fractured eastern terrace",world,VeilState.Broken,objects);
            for(int i=0;i<3;i++)
                Box("Separated terrace slab",brokenPlatform,new Vector3(8.65f+i*1.35f,.2f,6+(i==1?.17f:0)),new Vector3(1.2f,.4f,3.7f),stone,true);
            Shard("Terrace edge crystal",brokenPlatform,new Vector3(11.4f,.85f,7.5f),new Vector3(.3f,1.1f,.3f),violet);
            return objects;
        }
        void Arch(Transform parent,float z,float height,bool complete)
        {
            var arch=art.Group(complete ? "North / guardian triumph arch" : "South / broken arrival arch",parent,new Vector3(0,0,z));
            foreach(int side in new[]{-1,1})
            {
                Box("Arch pier",arch,new Vector3(side*3,height*.5f,0),new Vector3(1.2f,height,1.4f),stone,true);
                for(int i=0;i<4;i++) Part("Pier facing block",arch,new Vector3(side*3,.6f+i*(height/4),-.1f),new Vector3(1.42f,.34f,1.55f),trim);
            }
            // Radial voussoirs rather than a horizontal rectangular lintel. The north crown stays whole.
            for(int i=0;i<9;i++)
            {
                if(!complete && (i==3 || i==4)) continue;
                float a=(i*18+18)*Mathf.Deg2Rad;
                var t=Box("Arch wedge stone",arch,new Vector3(Mathf.Cos(a)*3,height+Mathf.Sin(a)*2.1f,0),new Vector3(1.12f,.85f,1.45f),trim,true);
                t.localRotation=Quaternion.Euler(0,0,i*18+18-90);
            }
        }
    }
}
