﻿using Beta.Platform.Messaging;

namespace Beta.Famicom.CPU
{
    public sealed class Tri
    {
        private readonly TriState tri;

        public Tri(State state)
        {
            this.tri = state.r2a03.tri;
        }

        public void Consume(ClockSignal e)
        {
            tri.timer--;

            if (tri.timer == 0)
            {
                tri.timer = tri.period + 1;

                if (tri.duration.counter != 0 && tri.linear_counter != 0)
                {
                    tri.step = (tri.step + 1) & 31;
                }
            }
        }
    }
}
