﻿using Beta.Platform;
using Beta.Platform.Core;

namespace Beta.SuperFamicom.PPU
{
    public sealed partial class Ppu : Processor
    {
        private static int[][][] priorityLut = new[]
        {
            new[] { new[] { 8, 11 }, new[] { 7, 10 }, new[] { 2,  5 }, new[] { 1, 4 }, new[] { 3, 6, 9, 12 } }, // mode 0
            new[] { new[] { 6,  9 }, new[] { 5,  8 }, new[] { 1,  3 }, new[] { 0, 0 }, new[] { 2, 4, 7, 10 } }, // mode 1
            new[] { new[] { 0,  0 }, new[] { 0,  0 }, new[] { 0,  0 }, new[] { 0, 0 }, new[] { 0, 0, 0,  0 } },
            new[] { new[] { 0,  0 }, new[] { 0,  0 }, new[] { 0,  0 }, new[] { 0, 0 }, new[] { 0, 0, 0,  0 } },
            new[] { new[] { 0,  0 }, new[] { 0,  0 }, new[] { 0,  0 }, new[] { 0, 0 }, new[] { 0, 0, 0,  0 } },
            new[] { new[] { 0,  0 }, new[] { 0,  0 }, new[] { 0,  0 }, new[] { 0, 0 }, new[] { 0, 0, 0,  0 } },
            new[] { new[] { 0,  0 }, new[] { 0,  0 }, new[] { 0,  0 }, new[] { 0, 0 }, new[] { 0, 0, 0,  0 } },
            new[] { new[] { 2,  3 }, new[] { 0,  0 }, new[] { 0,  0 }, new[] { 0, 0 }, new[] { 1, 4, 5,  6 } },
            new[] { new[] { 5,  8 }, new[] { 4,  7 }, new[] { 1, 10 }, new[] { 0, 0 }, new[] { 2, 3, 6,  9 } }  // mode 1 priority
        };

        private Driver gameSystem;
        private Register32 hLatch;
        private Register32 vLatch;
        private Register32 product;
        private bool forceBlank;
        private bool interlace;
        private bool overscan;
        private bool pseudoHi;
        private bool[] mathEnable = new bool[6];
        private byte ppu1Open;
        private byte ppu2Open;
        private byte ppu1Stat = 1;
        private byte ppu2Stat = 2;
        private byte vramLatch;
        private int forceMainToBlack;
        private int fixedColor;
        private int brightness;
        private int hclock;
        private int vclock;
        private int[] colors = colorLookup[0];
        private int[] raster;
        private int colorMathEnabled;

        static Ppu()
        {
            for (var brightness = 0; brightness < 16; brightness++)
            {
                for (var colour = 0; colour < 32768; colour++)
                {
                    var r = (colour << 3) & 0xf8;
                    var g = (colour >> 2) & 0xf8;
                    var b = (colour >> 7) & 0xf8;

                    // apply gradient to lower bits (this will make black=$000000 and white=$ffffff)
                    r |= (r >> 5);
                    g |= (r >> 5);
                    b |= (r >> 5);

                    r = (r * (brightness + 1)) / 16;
                    g = (g * (brightness + 1)) / 16;
                    b = (b * (brightness + 1)) / 16;

                    colorLookup[brightness][colour] = (r << 16) | (g << 8) | b;
                }
            }
        }

        public Ppu(Driver gameSystem)
        {
            Single = 4;

            this.gameSystem = gameSystem;
            bg0 = new Background(this, 0);
            bg1 = new Background(this, 1);
            bg2 = new Background(this, 2);
            bg3 = new Background(this, 3);
            clr = new ColorGeneration(this);
            spr = new Sprite(this);

            raster = new int[256];
        }

        public byte Peek2134()
        {
            product.sd0 = (short)Background.M7A.w * (sbyte)Background.M7B.h;

            return ppu1Open = product.ub0;
        }

        public byte Peek2135()
        {
            product.sd0 = (short)Background.M7A.w * (sbyte)Background.M7B.h;

            return ppu1Open = product.ub1;
        }

        public byte Peek2136()
        {
            product.sd0 = (short)Background.M7A.w * (sbyte)Background.M7B.h;

            return ppu1Open = product.ub2;
        }

        public byte Peek2137()
        {
            return 0;
        }

        public byte Peek213C()
        {
            return (hLatch.ub2 ^= 1) != 0 ?
                (ppu2Open = hLatch.ub0) :
                (ppu2Open = hLatch.ub1);
        }

        public byte Peek213D()
        {
            return (vLatch.ub2 ^= 1) != 0 ?
                (ppu2Open = vLatch.ub0) :
                (ppu2Open = vLatch.ub1);
        }

        public byte Peek213E()
        {
            return ppu1Stat;
        }

        public byte Peek213F()
        {
            var data = ppu2Stat;

            hLatch.ub2 = 0;
            vLatch.ub2 = 0;

            return data;
        }

        public void Poke2100(byte data)
        {
            forceBlank = (data & 0x80) != 0;
            brightness = (data & 0x0f);

            colors = colorLookup[brightness];
        }

        public void Poke2101(byte data)
        {
            spr.Addr = (data & 0x07) << 13;
            spr.Name = (data & 0x18) << 9;
            spr.Name += 0x1000;
            spr.Size = Sprite.SizeLut[(data & 0xe0) >> 5];
        }

        public void Poke2105(byte data)
        {
            Background.Mode = (data & 0x07);
            Background.Priority = (data & 0x08) != 0;
            bg0.CharSize = (data & 0x10) != 0 ? 16 : 8;
            bg1.CharSize = (data & 0x20) != 0 ? 16 : 8;
            bg2.CharSize = (data & 0x40) != 0 ? 16 : 8;
            bg3.CharSize = (data & 0x80) != 0 ? 16 : 8;

            var table = priorityLut[Background.Mode];

            if (Background.Mode == 1 && Background.Priority)
                table = priorityLut[8];

            bg0.Priorities = table[0];
            bg1.Priorities = table[1];
            bg2.Priorities = table[2];
            bg3.Priorities = table[3];
            spr.Priorities = table[4];
        }

        public void Poke2106(byte data)
        {
            bg0.Mosaic = (data & 0x01) != 0;
            bg1.Mosaic = (data & 0x02) != 0;
            bg2.Mosaic = (data & 0x04) != 0;
            bg3.Mosaic = (data & 0x08) != 0;

            Background.MosaicSize = (data & 0xf0) >> 4;
        }

        public void Poke2107(byte data)
        {
            bg0.NameSize = (data & 0x03);
            bg0.NameBase = (data & 0x7c) << 8;
        }

        public void Poke2108(byte data)
        {
            bg1.NameSize = (data & 0x03);
            bg1.NameBase = (data & 0x7c) << 8;
        }

        public void Poke2109(byte data)
        {
            bg2.NameSize = (data & 0x03);
            bg2.NameBase = (data & 0x7c) << 8;
        }

        public void Poke210A(byte data)
        {
            bg3.NameSize = (data & 0x03);
            bg3.NameBase = (data & 0x7c) << 8;
        }

        public void Poke210B(byte data)
        {
            bg0.CharBase = (data & 0x07) << 12;
            bg1.CharBase = (data & 0x70) << 8;
        }

        public void Poke210C(byte data)
        {
            bg2.CharBase = (data & 0x07) << 12;
            bg3.CharBase = (data & 0x70) << 8;
        }

        public void Poke210D(byte data)
        {
            bg0.WriteHOffset(data);

            Background.M7HOffset.l = Background.M7Latch;
            Background.M7HOffset.h = data;
            Background.M7Latch = data;
        }

        public void Poke210E(byte data)
        {
            bg0.WriteVOffset(data);

            Background.M7VOffset.l = Background.M7Latch;
            Background.M7VOffset.h = data;
            Background.M7Latch = data;
        }

        public void Poke210F(byte data)
        {
            bg1.WriteHOffset(data);
        }

        public void Poke2110(byte data)
        {
            bg1.WriteVOffset(data);
        }

        public void Poke2111(byte data)
        {
            bg2.WriteHOffset(data);
        }

        public void Poke2112(byte data)
        {
            bg2.WriteVOffset(data);
        }

        public void Poke2113(byte data)
        {
            bg3.WriteHOffset(data);
        }

        public void Poke2114(byte data)
        {
            bg3.WriteVOffset(data);
        }

        public void Poke2115(byte data)
        {
            vramCtrl = data;

            switch (vramCtrl & 3)
            {
            case 0: vramStep = 0x01; break;
            case 1: vramStep = 0x20; break;
            case 2: vramStep = 0x80; break;
            case 3: vramStep = 0x80; break;
            }
        }

        public void Poke211A(byte data)
        {
            Background.M7Control = data;
        }

        public void Poke211B(byte data)
        {
            Background.M7A.l = Background.M7Latch;
            Background.M7A.h = data;
            Background.M7Latch = data;
        }

        public void Poke211C(byte data)
        {
            Background.M7B.l = Background.M7Latch;
            Background.M7B.h = data;
            Background.M7Latch = data;
        }

        public void Poke211D(byte data)
        {
            Background.M7C.l = Background.M7Latch;
            Background.M7C.h = data;
            Background.M7Latch = data;
        }

        public void Poke211E(byte data)
        {
            Background.M7D.l = Background.M7Latch;
            Background.M7D.h = data;
            Background.M7Latch = data;
        }

        public void Poke211F(byte data)
        {
            Background.M7X.l = Background.M7Latch;
            Background.M7X.h = data;
            Background.M7Latch = data;
        }

        public void Poke2120(byte data)
        {
            Background.M7Y.l = Background.M7Latch;
            Background.M7Y.h = data;
            Background.M7Latch = data;
        }

        public void Poke2123(byte data)
        {
            bg0.PokeWindow1(data); data >>= 4;
            bg1.PokeWindow1(data);
        }

        public void Poke2124(byte data)
        {
            bg2.PokeWindow1(data); data >>= 4;
            bg3.PokeWindow1(data);
        }

        public void Poke2125(byte data)
        {
            spr.PokeWindow1(data); data >>= 4;
            clr.PokeWindow1(data);
        }

        public void Poke2126(byte data)
        {
            if (window1.L == data)
            {
                return;
            }

            window1.L = data;
            window1.Dirty = true;
        }

        public void Poke2127(byte data)
        {
            if (window1.R == data)
            {
                return;
            }

            window1.R = data;
            window1.Dirty = true;
        }

        public void Poke2128(byte data)
        {
            if (window2.L == data)
            {
                return;
            }

            window2.L = data;
            window2.Dirty = true;
        }

        public void Poke2129(byte data)
        {
            if (window2.R == data)
            {
                return;
            }

            window2.R = data;
            window2.Dirty = true;
        }

        public void Poke212A(byte data)
        {
            bg0.PokeWindow2(data); data >>= 2;
            bg1.PokeWindow2(data); data >>= 2;
            bg2.PokeWindow2(data); data >>= 2;
            bg3.PokeWindow2(data);
        }

        public void Poke212B(byte data)
        {
            spr.PokeWindow2(data); data >>= 2;
            clr.PokeWindow2(data);
        }

        public void Poke212C(byte data)
        {
            bg0.Sm = (data & 0x01) != 0 ? ~0 : 0;
            bg1.Sm = (data & 0x02) != 0 ? ~0 : 0;
            bg2.Sm = (data & 0x04) != 0 ? ~0 : 0;
            bg3.Sm = (data & 0x08) != 0 ? ~0 : 0;
            spr.Sm = (data & 0x10) != 0 ? ~0 : 0;
        }

        public void Poke212D(byte data)
        {
            bg0.Ss = (data & 0x01) != 0 ? ~0 : 0;
            bg1.Ss = (data & 0x02) != 0 ? ~0 : 0;
            bg2.Ss = (data & 0x04) != 0 ? ~0 : 0;
            bg3.Ss = (data & 0x08) != 0 ? ~0 : 0;
            spr.Ss = (data & 0x10) != 0 ? ~0 : 0;
        }

        public void Poke212E(byte data)
        {
            bg0.Wm = (data & 0x01) != 0;
            bg1.Wm = (data & 0x02) != 0;
            bg2.Wm = (data & 0x04) != 0;
            bg3.Wm = (data & 0x08) != 0;
            spr.Wm = (data & 0x10) != 0;
        }

        public void Poke212F(byte data)
        {
            bg0.Ws = (data & 0x01) != 0;
            bg1.Ws = (data & 0x02) != 0;
            bg2.Ws = (data & 0x04) != 0;
            bg3.Ws = (data & 0x08) != 0;
            spr.Ws = (data & 0x10) != 0;
        }

        public void Poke2130(byte data)
        {
            forceMainToBlack = (data & 0xc0) >> 6;
            colorMathEnabled = (data & 0x30) >> 4;
        }

        public void Poke2131(byte data)
        {
            mathEnable[0] = (data & 0x01) != 0;
            mathEnable[1] = (data & 0x02) != 0;
            mathEnable[2] = (data & 0x04) != 0;
            mathEnable[3] = (data & 0x08) != 0;
            mathEnable[4] = (data & 0x10) != 0;
            mathEnable[5] = (data & 0x20) != 0;
        }

        public void Poke2132(byte data)
        {
            if ((data & 0x80) != 0) { fixedColor = (fixedColor & ~0x7c00) | ((data & 0x1f) << 10); }
            if ((data & 0x40) != 0) { fixedColor = (fixedColor & ~0x03e0) | ((data & 0x1f) <<  5); }
            if ((data & 0x20) != 0) { fixedColor = (fixedColor & ~0x001f) | ((data & 0x1f) <<  0); }
        }

        public void Poke2133(byte data)
        {
            // todo: mode7 extbg bit
            pseudoHi = (data & 0x08) != 0;
            overscan = (data & 0x04) != 0;
            spr.Interlace = (data & 0x02) != 0;
            interlace = (data & 0x01) != 0;
        }

        public void Initialize()
        {
            bg0.Initialize();
            bg1.Initialize();
            bg2.Initialize();
            bg3.Initialize();
            clr.Initialize();
            spr.Initialize();
        }

        public override void Update()
        {
            hclock++;
            
            if (hclock == 274) { RenderScanline(); }
            if (hclock == 341)
            {
                hclock = 0;
                vclock++;

                if (vclock == (overscan ? 241 : 225))
                {
                    ppu2Stat ^= 0x80; // toggle field flag every vblank
                }

                if (vclock == 262)
                {
                    vclock = 0;

                    ppu1Stat &= 0x3F; // reset time and range flags

                    gameSystem.Video.Render();
                    gameSystem.Joypad1.Update();
                    gameSystem.Joypad2.Update();
                }

                if (vclock < 240)
                {
                    raster = gameSystem.Video.GetRaster(vclock);
                }
            }
        }

        private void RenderScanline()
        {
            for (var i = 0; i < 256; i++)
            {
                bg0.Enable[i] = false;
                bg1.Enable[i] = false;
                bg2.Enable[i] = false;
                bg3.Enable[i] = false;
                spr.Enable[i] = false;
                raster[i] = 0;
            }

            if (forceBlank || vclock > (overscan ? 0xef : 0xdf))
            {
                return;
            }

            if (window1.Dirty)
            {
                window1.Dirty = false;
                window1.Update();

                // invalidate all layers using window 1
                bg0.WnDirty |= bg0.W1 != 0;
                bg1.WnDirty |= bg1.W1 != 0;
                bg2.WnDirty |= bg2.W1 != 0;
                bg3.WnDirty |= bg3.W1 != 0;
                spr.WnDirty |= spr.W1 != 0;
                clr.WnDirty |= clr.W1 != 0;
            }

            if (window2.Dirty)
            {
                window2.Dirty = false;
                window2.Update();

                // invalidate all layers using window 2
                bg0.WnDirty |= bg0.W2 != 0;
                bg1.WnDirty |= bg1.W2 != 0;
                bg2.WnDirty |= bg2.W2 != 0;
                bg3.WnDirty |= bg3.W2 != 0;
                spr.WnDirty |= spr.W2 != 0;
                clr.WnDirty |= clr.W2 != 0;
            }

            if (bg0.WnDirty) { bg0.WnDirty = false; bg0.UpdateWindow(); }
            if (bg1.WnDirty) { bg1.WnDirty = false; bg1.UpdateWindow(); }
            if (bg2.WnDirty) { bg2.WnDirty = false; bg2.UpdateWindow(); }
            if (bg3.WnDirty) { bg3.WnDirty = false; bg3.UpdateWindow(); }
            if (spr.WnDirty) { spr.WnDirty = false; spr.UpdateWindow(); }
            if (clr.WnDirty) { clr.WnDirty = false; clr.UpdateWindow(); }

            switch (Background.Mode)
            {
            case 0: RenderMode0(); break;
            case 1: RenderMode1(); break;
            case 2: RenderMode2(); break;
            case 3: RenderMode3(); break;
            case 4: RenderMode4(); break;
            case 5: RenderMode5(); break;
            case 6: RenderMode6(); break;
            case 7: RenderMode7(); break; // affine render
            }
        }
    }
}
