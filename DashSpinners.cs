﻿using Celeste;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;
using System;
using System.Collections.Generic;

namespace Celeste.Mod.IsaGrabBag
{
    public class DreamSpinner : CrystalStaticSpinner
    {
        private static bool dreamdashEnabled = true;
        private DreamBlock block;
        private bool hasDashed, fake;

        public List<DreamSpinner> spinners;
        public List<Vector2> fillOffset;

        public int RNG;

        public bool Fragile;

        public DreamSpinner(EntityData data, Vector2 offset, bool _fake)
            : this(data.Position + offset, data.Bool("useOnce", false), _fake)
        {
        }

        public DreamSpinner(Vector2 position, bool _useOnce, bool _fake)
            : base(position, false, CrystalColor.Red)
        {
            spinners = new List<DreamSpinner>();
            fillOffset = new List<Vector2>();

            Fragile = _useOnce;
            fake = _fake;
            Visible = false;
            RNG = Calc.Random.Next(4);
            Depth = -8500;
            Collidable = false;
        }

        public override void Awake(Scene scene)
        {
        }
        public override void Update()
        {
            if (fake)
                return;

            Player player = Scene.Entities.FindFirst<Player>();

            if (player != null)
            {
                dreamdashEnabled = player.Inventory.DreamDash;
                if (Fragile)
                {
                    bool isColliding = block.Collidable && player.Collider.Collide(block);

                    if (!isColliding && hasDashed)
                    {
                        RemoveSelf();
                        block.RemoveSelf();

                        Audio.Play("event:/game/06_reflection/fall_spike_smash", Position);
                        Audio.Play("event:/game/06_reflection/fall_spike_smash", Position);
                        Audio.Play("event:/game/06_reflection/fall_spike_smash", Position);
                        CrystalDebris.Burst(Position, new Color(0xC1, 0x8A, 0x53), false, 4); //c18a53
                        return;
                    }
                    hasDashed = isColliding;
                }
                if ((player.DashAttacking || player.StateMachine.State == 9) && dreamdashEnabled)
                {
                    block.Collidable = true;
                    Collidable = false;
                }
                else
                {
                    block.Collidable = false;
                    Collidable = true;
                }
            }
        }

        public override void Removed(Scene scene)
        {
            base.Removed(scene);
        }

        public override void Added(Scene scene)
        {
            base.Added(scene);

            if (!DreamSpinnerBorder.addedToScene)
            {
                DreamSpinnerBorder.addedToScene = true;
                scene.Add(new DreamSpinnerBorder());
            }

            if (!Fragile)
            {
                foreach (var spin in SceneAs<Level>().Entities.FindAll<DreamSpinner>())
                {
                    if (spin == this || spin.Fragile || spin.spinners.Contains(this))
                        continue;

                    if (Vector2.Distance(spin.Position, Position) < 24)
                    {
                        spinners.Add(spin);
                        fillOffset.Add((spin.Center + Center) / 2 - (Vector2.One * 5));
                    }
                }
            }

            if (!fake)
            {
                scene.Add(block = new DreamBlock(Center - new Vector2(8, 8), 16, 16, null, false, false, false));
                block.Visible = false;
            }
        }

        public bool InView()
        {
            Camera camera = (Scene as Level).Camera;
            return X > camera.X - 16f && Y > camera.Y - 16f && X < camera.X + 320f + 16f && Y < camera.Y + 180f + 16f;
        }
    }

    struct Star
    {
        public Star(Vector2 _pos, int _depth, float _animSpeed, float _initAnim, byte _color)
        {
            point = _pos;
            depth = _depth;
            animSpeed = _animSpeed;
            anim = _initAnim;
            color = _color;
        }
        public Vector2 point;
        public int depth;
        public byte color;
        public float anim, animSpeed;
    }
    class DreamSpinnerBorder : Entity
    {
        public static bool addedToScene = false;
        
        public const int SPIN_TYPE_COUNT = 4;
        public const int SPRITE_MAIN = 21, SPRITE_FILLER = 14;

        const int starSize = 1500;
        const int depthDiv = starSize / 4;

        private const int WIDTH = 320 + 4, HEIGHT = 180 + 4, TEX_SIZE = WIDTH * HEIGHT;
        private Texture2D texture;
        private Vector2 camPos, lastCamPos;
        private Rectangle rect;

        private byte[] colors = new byte[WIDTH * HEIGHT * 4], baseTex = new byte[WIDTH * HEIGHT];
        public static byte[][] spinners = new byte[SPIN_TYPE_COUNT][];
        public static byte[] fillers = new byte[SPRITE_FILLER * SPRITE_FILLER];

        private Star[] stars;

        private static byte[] colorIndexes = new byte[]
        {
            0xFF, 0xFF, 0xFF, 0xFF,
            0x00, 0x00, 0x00, 0xFF,
            0x63, 0x83, 0xB8, 0xFF, // 6383b8
            0xBA, 0x4A, 0x4A, 0xFF, // ba4a4a
            0xF0, 0xFF, 0x00, 0xFF, // f0ff00
            0x98, 0x00, 0xB2, 0xFF, // 9800b2
            0x8C, 0xC5, 0x82, 0xFF, // 8cc582
            0x3C, 0xA3, 0x1D, 0xFF, // 3ca31d
            0x72, 0xE0, 0xEA, 0xFF, // 72e0ea
            
            // Fragile
            0xBD, 0x9E, 0x7F, 0xFF, // bd9e7f
            0x22, 0x14, 0x03, 0xFF, // 3d2507
            0x66, 0xB8, 0x63, 0xFF, // 66b863
            0xBA, 0x4A, 0x4A, 0xFF, // ba4a4a
            0xFF, 0xC8, 0x00, 0xFF, // ffc800
            0x00, 0xB2, 0x21, 0xFF, // 00b221
            0xC5, 0x95, 0x82, 0xFF, // c59582
            0xA3, 0x1D, 0x2C, 0xFF, // a31d2c
            0xBA, 0xEA, 0x72, 0xFF, // baea72
            
            // Disabled
            0xDD, 0xDD, 0xDD, 0xFF, // bd9e7f
            0x66, 0x66, 0x66, 0xFF, // 3d2507
            0x99, 0x99, 0x99, 0xFF, // 66b863
            0x99, 0x99, 0x99, 0xFF, // 66b863
            0x99, 0x99, 0x99, 0xFF, // 66b863
            0x99, 0x99, 0x99, 0xFF, // 66b863
            0x99, 0x99, 0x99, 0xFF, // 66b863
            0x99, 0x99, 0x99, 0xFF, // 66b863
            0x99, 0x99, 0x99, 0xFF, // 66b863
        };

        public DreamSpinnerBorder()
        {
            Depth = 500;

            texture = new Texture2D(Draw.SpriteBatch.GraphicsDevice, WIDTH, HEIGHT);
        }

        public override void Awake(Scene scene)
        {
            addedToScene = false;
            Update();
            base.Awake(scene);
        }
        public override void Added(Scene scene)
        {
            base.Added(scene);

            stars = new Star[starSize];

            int RNG_seed = (int)SceneAs<Level>().LevelOffset.X & 0xFFF;
            RNG_seed |= ((int)SceneAs<Level>().LevelOffset.Y & 0xFFF) << 12;
            Calc.PushRandom(RNG_seed);

            for (int i = 0; i < starSize; ++i)
            {
                stars[i] = new Star(new Vector2(Calc.Random.NextFloat(WIDTH), Calc.Random.NextFloat(HEIGHT)), i / depthDiv, Calc.Random.NextFloat(1f) + 5f, Calc.Random.NextFloat(4f), (byte)Calc.Random.Next(0, 7));
            }

            camPos = SceneAs<Level>().Camera.Position;
            camPos.X = (int)camPos.X;
            camPos.Y = (int)camPos.Y;
            lastCamPos = camPos;

            Calc.PopRandom();
        }

        public override void Update()
        {
            Array.Clear(baseTex, 0, TEX_SIZE);
            Array.Clear(colors, 0, TEX_SIZE * 4);

            camPos = SceneAs<Level>().Camera.Position;
            camPos.X = (int)camPos.X;
            camPos.Y = (int)camPos.Y;
            rect = new Rectangle((int)camPos.X - 2, (int)camPos.Y - 2, WIDTH, HEIGHT);

            byte c;

            for (int i = 0; i < starSize; ++i)
            {
                stars[i].point -= (camPos - lastCamPos) * (5 - stars[i].depth) * 0.15f;

                while (stars[i].point.X >= WIDTH - 2)
                    stars[i].point.X -= WIDTH - 4;
                while (stars[i].point.X < 2)
                    stars[i].point.X += WIDTH - 4;
                while (stars[i].point.Y >= HEIGHT - 2)
                    stars[i].point.Y -= HEIGHT - 4;
                while (stars[i].point.Y < 2)
                    stars[i].point.Y += HEIGHT - 4;

                c = stars[i].color;

                if (stars[i].depth < 2)
                {
                    stars[i].anim += stars[i].animSpeed * Engine.DeltaTime;
                    if (stars[i].anim >= 4)
                        stars[i].anim -= 4;
                }

                if (stars[i].depth == 0 && stars[i].anim >= 2 && stars[i].anim < 3)
                {
                    int index = ((int)stars[i].point.Y - 2) * WIDTH + (int)stars[i].point.X;

                    baseTex[index] = c;
                    baseTex[index + (WIDTH << 2)] = c;

                    index += WIDTH - 1;
                    for (int j = 0; j < 3; ++j)
                    {
                        baseTex[index + j] = c;
                        baseTex[index + j + (WIDTH << 1)] = c;
                    }
                    index += WIDTH - 1;

                    for (int j = 0; j < 2; ++j)
                    {
                        baseTex[index + j] = c;
                        baseTex[index + j + 3] = c;
                    }
                }
                else if ((stars[i].depth == 0 && stars[i].anim >= 1) || (stars[i].depth == 1 && stars[i].anim % 2 >= 1))
                {
                    int index = ((int)stars[i].point.Y - 1) * WIDTH + (int)stars[i].point.X;

                    baseTex[index] = c;
                    baseTex[index + (WIDTH << 1)] = c;

                    index += WIDTH - 1;
                    for (int j = 0; j < 3; ++j)
                    {
                        baseTex[index + j] = c;
                    }
                }
                else
                {
                    baseTex[((int)stars[i].point.Y) * WIDTH + (int)stars[i].point.X] = c;
                }
            }

            lastCamPos = camPos;

            int x, xMax, y, yMax, type = 0;
            int u, v, offset = 0;


            bool dreamdashEnabled = true;
            Player player = Scene.Entities.FindFirst<Player>();

            if (player != null)
                dreamdashEnabled = player.Inventory.DreamDash;


            foreach (var spin in Scene.Entities.FindAll<DreamSpinner>())
            {
                if (!spin.InView())
                    continue;
                type = spin.RNG;

                offset = spin.Fragile ? 9 : 0;
                if (!dreamdashEnabled)
                    offset = 18;

                xMax = Math.Min((int)(spin.X - camPos.X - 8) + SPRITE_MAIN, WIDTH);
                yMax = Math.Min((int)(spin.Y - camPos.Y - 8) + SPRITE_MAIN, HEIGHT);

                u = -Math.Min((int)(spin.X - camPos.X - 8), 0);

                for (x = Math.Max((int)(spin.X - camPos.X - 8), 0); x < xMax; ++x)
                {
                    v = -Math.Min((int)(spin.Y - camPos.Y - 8), 0);
                    for (y = Math.Max((int)(spin.Y - camPos.Y - 8), 0); y < yMax; ++y)
                    {
                        c = spinners[type][(u * SPRITE_MAIN) + v];
                        ++v;

                        //Logger.Log("IsaBag", u.ToString() + ", " + v.ToString());

                        // if pixel is transparent, or pixel is the border where pixels are colored in, ignore (to prevent border showing up in the middle)
                        if (c == 0 || (c == 1 && colors[((y * WIDTH + x) << 2) + 3] != 0))
                        {
                            continue;
                        }

                        if (c == 2)
                        {
                            c = (byte)(baseTex[y * WIDTH + x] + 2);
                            
                        }
                        c = (byte)(offset + c);

                        Array.Copy(colorIndexes, (c - 1) << 2, colors, (y * WIDTH + x) << 2, 4);

                    }
                    ++u;
                }

                foreach (Vector2 pos in spin.fillOffset)
                {
                    xMax = Math.Min((int)(pos.X - camPos.X) + SPRITE_FILLER, WIDTH);
                    yMax = Math.Min((int)(pos.Y - camPos.Y) + SPRITE_FILLER, HEIGHT);

                    u = -Math.Min((int)(pos.X - camPos.X), 0);

                    for (x = Math.Max((int)(pos.X - camPos.X), 0); x < xMax; ++x)
                    {
                        v = -Math.Min((int)(pos.Y - camPos.Y), 0);
                        for (y = Math.Max((int)(pos.Y - camPos.Y), 0); y < yMax; ++y)
                        {
                            c = fillers[(u * SPRITE_FILLER) + v];
                            ++v;

                            // if pixel is transparent, or pixel is the border where pixels are colored in, ignore (to prevent border showing up in the middle)
                            if (c == 0 || (c == 1 && colors[((y * WIDTH + x) << 2) + 3] != 0))
                            {
                                continue;
                            }

                            if (c == 2)
                            {
                                c = (byte)(baseTex[y * WIDTH + x] + 2);
                            }
                            c = (byte)(offset + c);

                            Array.Copy(colorIndexes, (c - 1) << 2, colors, (y * WIDTH + x) << 2, 4);

                        }
                        ++u;
                    }
                }

            }

            texture.SetData(colors);

            base.Update();
        }

        public override void Render()
        {
            if (SceneAs<Level>().Transitioning)
                Update();

            Draw.SpriteBatch.Draw(texture, rect, Color.White);
        }
    }
}