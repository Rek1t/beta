﻿using Beta.GameBoy.APU;

namespace Beta.GameBoy
{
    public sealed class State
    {
        public ApuState apu = new ApuState();
        public CpuState cpu = new CpuState();
        public PadState pad = new PadState();
        public PpuState ppu = new PpuState();
        public TmaState tma = new TmaState();

        public bool boot_rom_enabled = true;
    }

    public sealed class CpuState
    {
        public byte ief;
        public byte irf;
    }

    public sealed class PadState
    {
        public bool p14;
        public bool p15;
        public byte p14_latch;
        public byte p15_latch;
    }

    public sealed class PpuState
    {
        public bool bkg_enabled;
        public bool lcd_enabled;
        public bool obj_enabled;
        public bool wnd_enabled;
        public byte bkg_palette;
        public byte[] obj_palette = new byte[2];
        public byte scroll_x;
        public byte scroll_y;
        public byte window_x;
        public byte window_y;
        public int bkg_char_address = 0x1000;
        public int bkg_name_address = 0x1800;
        public int wnd_name_address = 0x1800;
        public int obj_rasters = 8;
        public int control;
        public int h;
        public byte v;
        public byte v_check;
        public byte ff40;

        public bool dma_trigger;
        public byte dma_segment;
    }

    public sealed class TmaState
    {
        public int divider;
        public int counter;
        public int control;
        public int modulus;
    }
}
