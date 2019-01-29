﻿//-----------------------------------------------------------------------------
// FILE:	    Test_ByteUnits.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Xunit;

using Xunit;

namespace TestCommon
{
    public class Test_ByteUnits
    {
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void ParseBase2()
        {
            // Verify that the units are correct.

            Assert.Equal(Math.Pow(2, 10), ByteUnits.KibiBytes);
            Assert.Equal(Math.Pow(2, 20), ByteUnits.MebiBytes);
            Assert.Equal(Math.Pow(2, 30), ByteUnits.GibiBytes);
            Assert.Equal(Math.Pow(2, 40), ByteUnits.TebiBytes);
            Assert.Equal(Math.Pow(2, 50), ByteUnits.PebiBytes);

            double value;

            // Parse whole values.

            Assert.True(ByteUnits.TryParseCount("0", out value));
            Assert.Equal(0.0, value);

            Assert.True(ByteUnits.TryParseCount("4kib", out value));
            Assert.Equal((double)ByteUnits.KibiBytes * 4, value);

            Assert.True(ByteUnits.TryParseCount("4mib", out value));
            Assert.Equal((double)ByteUnits.MebiBytes * 4, value);

            Assert.True(ByteUnits.TryParseCount("7GiB", out value));
            Assert.Equal((double)ByteUnits.GibiBytes * 7, value);

            Assert.True(ByteUnits.TryParseCount("2TiB", out value));
            Assert.Equal((double)ByteUnits.TebiBytes * 2, value);

            Assert.True(ByteUnits.TryParseCount("2GiB", out value));
            Assert.Equal((double)ByteUnits.GibiBytes * 2, value);

            Assert.True(ByteUnits.TryParseCount("4tib", out value));
            Assert.Equal((double)ByteUnits.TebiBytes * 4, value);

            Assert.True(ByteUnits.TryParseCount("3pib", out value));
            Assert.Equal((double)ByteUnits.PebiBytes * 3, value);

            // Test fractional values.

            Assert.True(ByteUnits.TryParseCount("0.5", out value));
            Assert.Equal(0.5, value);

            Assert.True(ByteUnits.TryParseCount("0.5B", out value));
            Assert.Equal(0.5, value);

            Assert.True(ByteUnits.TryParseCount("1.5KiB", out value));
            Assert.Equal((double)ByteUnits.KibiBytes * 1.5, value);

            Assert.True(ByteUnits.TryParseCount("1.5MiB", out value));
            Assert.Equal((double)ByteUnits.MebiBytes * 1.5, value);

            Assert.True(ByteUnits.TryParseCount("1.5GiB", out value));
            Assert.Equal((double)ByteUnits.GibiBytes * 1.5, value);

            Assert.True(ByteUnits.TryParseCount("1.5TiB", out value));
            Assert.Equal((double)ByteUnits.TebiBytes * 1.5, value);

            Assert.True(ByteUnits.TryParseCount("1.5PiB", out value));
            Assert.Equal((double)ByteUnits.PebiBytes * 1.5, value);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void ParseBase10()
        {
            // Verify that the units are correct.

            Assert.Equal(1000L, ByteUnits.KiloBytes);
            Assert.Equal(1000000L, ByteUnits.MegaBytes);
            Assert.Equal(1000000000L, ByteUnits.GigaBytes);
            Assert.Equal(1000000000000L, ByteUnits.TeraBytes);
            Assert.Equal(1000000000000000L, ByteUnits.PentaBytes);

            double value;

            // Parse whole values.

            Assert.True(ByteUnits.TryParseCount("0", out value));
            Assert.Equal(0.0, value);

            Assert.True(ByteUnits.TryParseCount("10b", out value));
            Assert.Equal(10.0, value);

            Assert.True(ByteUnits.TryParseCount("20B", out value));
            Assert.Equal(20.0, value);

            Assert.True(ByteUnits.TryParseCount("1K", out value));
            Assert.Equal((double)ByteUnits.KiloBytes, value);

            Assert.True(ByteUnits.TryParseCount("2KB", out value));
            Assert.Equal((double)ByteUnits.KiloBytes * 2, value);

            Assert.True(ByteUnits.TryParseCount("3k", out value));
            Assert.Equal((double)ByteUnits.KiloBytes * 3, value);

            Assert.True(ByteUnits.TryParseCount("4kb", out value));
            Assert.Equal((double)ByteUnits.KiloBytes * 4, value);

            Assert.True(ByteUnits.TryParseCount("1M", out value));
            Assert.Equal((double)ByteUnits.MegaBytes, value);

            Assert.True(ByteUnits.TryParseCount("2MB", out value));
            Assert.Equal((double)ByteUnits.MegaBytes * 2, value);

            Assert.True(ByteUnits.TryParseCount("3m", out value));
            Assert.Equal((double)ByteUnits.MegaBytes * 3, value);

            Assert.True(ByteUnits.TryParseCount("4mb", out value));
            Assert.Equal((double)ByteUnits.MegaBytes * 4, value);

            Assert.True(ByteUnits.TryParseCount("1G", out value));
            Assert.Equal((double)ByteUnits.GigaBytes, value);

            Assert.True(ByteUnits.TryParseCount("2TB", out value));
            Assert.Equal((double)ByteUnits.TeraBytes * 2, value);

            Assert.True(ByteUnits.TryParseCount("1T", out value));
            Assert.Equal((double)ByteUnits.TeraBytes, value);

            Assert.True(ByteUnits.TryParseCount("2GB", out value));
            Assert.Equal((double)ByteUnits.GigaBytes * 2, value);

            Assert.True(ByteUnits.TryParseCount("3g", out value));
            Assert.Equal((double)ByteUnits.GigaBytes * 3, value);

            Assert.True(ByteUnits.TryParseCount("4gb", out value));
            Assert.Equal((double)ByteUnits.GigaBytes * 4, value);

            Assert.True(ByteUnits.TryParseCount("3t", out value));
            Assert.Equal((double)ByteUnits.TeraBytes * 3, value);

            Assert.True(ByteUnits.TryParseCount("4tb", out value));
            Assert.Equal((double)ByteUnits.TeraBytes * 4, value);

            Assert.True(ByteUnits.TryParseCount("3p", out value));
            Assert.Equal((double)ByteUnits.PentaBytes * 3, value);

            Assert.True(ByteUnits.TryParseCount("4pb", out value));
            Assert.Equal((double)ByteUnits.PentaBytes * 4, value);

            // Test fractional values.

            Assert.True(ByteUnits.TryParseCount("0.5", out value));
            Assert.Equal(0.5, value);

            Assert.True(ByteUnits.TryParseCount("0.5B", out value));
            Assert.Equal(0.5, value);

            Assert.True(ByteUnits.TryParseCount("1.5KB", out value));
            Assert.Equal((double)ByteUnits.KiloBytes * 1.5, value);

            Assert.True(ByteUnits.TryParseCount("1.5MB", out value));
            Assert.Equal((double)ByteUnits.MegaBytes * 1.5, value);

            Assert.True(ByteUnits.TryParseCount("1.5GB", out value));
            Assert.Equal((double)ByteUnits.GigaBytes * 1.5, value);

            Assert.True(ByteUnits.TryParseCount("1.5TB", out value));
            Assert.Equal((double)ByteUnits.TeraBytes * 1.5, value);

            Assert.True(ByteUnits.TryParseCount("1.5PB", out value));
            Assert.Equal((double)ByteUnits.PentaBytes * 1.5, value);
        }

        [Fact]
        public void ParseErrors()
        {
            double value;

            Assert.False(ByteUnits.TryParseCount(null, out value));
            Assert.False(ByteUnits.TryParseCount("", out value));
            Assert.False(ByteUnits.TryParseCount("   ", out value));
            Assert.False(ByteUnits.TryParseCount("ABC", out value));
            Assert.False(ByteUnits.TryParseCount("-10", out value));
            Assert.False(ByteUnits.TryParseCount("-20KB", out value));
            Assert.False(ByteUnits.TryParseCount("10a", out value));
            Assert.False(ByteUnits.TryParseCount("10akb", out value));
        }
    }
}
