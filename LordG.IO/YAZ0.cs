﻿using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using Syroot.BinaryData;
using System.Linq;

namespace LordG.IO
{
    public static class YAZ0
    {

		public const uint MagicLE = 0x59617A30;

		public const uint MagicBe = 0x307A6159;

		public static void Decompress(ref byte[] data)
		{
			if (data[0] != 'Y' || data[1] != 'a' || data[2] != 'z' || data[3] != '0')
				return;

			int fullsize = (data[4] << 24) | (data[5] << 16) | (data[6] << 8) | data[7];
			byte[] output = new byte[fullsize];

			int inpos = 16, outpos = 0;
			while (outpos < fullsize)
			{
				byte block = data[inpos++];

				for (int i = 0; i < 8; i++)
				{
					if ((block & 0x80) != 0)
					{
						// copy one plain byte
						output[outpos++] = data[inpos++];
					}
					else
					{
						// copy N compressed bytes
						byte b1 = data[inpos++];
						byte b2 = data[inpos++];

						int dist = ((b1 & 0xF) << 8) | b2;
						int copysrc = outpos - (dist + 1);

						int nbytes = b1 >> 4;
						if (nbytes == 0) nbytes = data[inpos++] + 0x12;
						else nbytes += 2;

						for (int j = 0; j < nbytes; j++)
							output[outpos++] = output[copysrc++];
					}

					block <<= 1;
					if (outpos >= fullsize || inpos >= data.Length)
						break;
				}
			}

			Array.Resize(ref data, fullsize);
			output.CopyTo(data, 0);
		}

		public static EndianStream Decompress(EndianStream stream, bool dispose = false)
        {
			var buf = stream.ToArray();
			Decompress(ref buf);
			if (dispose)
				stream.Dispose();
			return new EndianStream(buf);
        }

		public static unsafe byte[] Compress(byte[] Data)
		{
			byte* dataptr = (byte*)Marshal.UnsafeAddrOfPinnedArrayElement(Data, 0);

			byte[] result = new byte[Data.Length + Data.Length / 8 + 0x10];
			byte* resultptr = (byte*)Marshal.UnsafeAddrOfPinnedArrayElement(result, 0);
			*resultptr++ = (byte)'Y';
			*resultptr++ = (byte)'a';
			*resultptr++ = (byte)'z';
			*resultptr++ = (byte)'0';
			*resultptr++ = (byte)((Data.Length >> 24) & 0xFF);
			*resultptr++ = (byte)((Data.Length >> 16) & 0xFF);
			*resultptr++ = (byte)((Data.Length >> 8) & 0xFF);
			*resultptr++ = (byte)((Data.Length >> 0) & 0xFF);
			for (int i = 0; i < 8; i++) *resultptr++ = 0;
			int length = Data.Length;
			int dstoffs = 16;
			int Offs = 0;
			while (true)
			{
				int headeroffs = dstoffs++;
				resultptr++;
				byte header = 0;
				for (int i = 0; i < 8; i++)
				{
					int comp = 0;
					int back = 1;
					int nr = 2;
					{
						byte* ptr = dataptr - 1;
						int maxnum = 0x111;
						if (length - Offs < maxnum) maxnum = length - Offs;
						//Use a smaller amount of bytes back to decrease time
						int maxback = 0x400;//0x1000;
						if (Offs < maxback) maxback = Offs;
						maxback = (int)dataptr - maxback;
						int tmpnr;
						while (maxback <= (int)ptr)
						{
							if (*(ushort*)ptr == *(ushort*)dataptr && ptr[2] == dataptr[2])
							{
								tmpnr = 3;
								while (tmpnr < maxnum && ptr[tmpnr] == dataptr[tmpnr]) tmpnr++;
								if (tmpnr > nr)
								{
									if (Offs + tmpnr > length)
									{
										nr = length - Offs;
										back = (int)(dataptr - ptr);
										break;
									}
									nr = tmpnr;
									back = (int)(dataptr - ptr);
									if (nr == maxnum) break;
								}
							}
							--ptr;
						}
					}
					if (nr > 2)
					{
						Offs += nr;
						dataptr += nr;
						if (nr >= 0x12)
						{
							*resultptr++ = (byte)(((back - 1) >> 8) & 0xF);
							*resultptr++ = (byte)((back - 1) & 0xFF);
							*resultptr++ = (byte)((nr - 0x12) & 0xFF);
							dstoffs += 3;
						}
						else
						{
							*resultptr++ = (byte)((((back - 1) >> 8) & 0xF) | (((nr - 2) & 0xF) << 4));
							*resultptr++ = (byte)((back - 1) & 0xFF);
							dstoffs += 2;
						}
						comp = 1;
					}
					else
					{
						*resultptr++ = *dataptr++;
						dstoffs++;
						Offs++;
					}
					header = (byte)((header << 1) | ((comp == 1) ? 0 : 1));
					if (Offs >= length)
					{
						header = (byte)(header << (7 - i));
						break;
					}
				}
				result[headeroffs] = header;
				if (Offs >= length) break;
			}
			while ((dstoffs % 4) != 0) dstoffs++;
			byte[] realresult = new byte[dstoffs];
			Array.Copy(result, realresult, dstoffs);
			return realresult;
		}

		public static bool CheckMagic(byte[] src, ByteOrder order)
        {
			using (EndianStream stream = src)
            {
				var num = stream.ReadUInt(order);
				return num is MagicLE || num is MagicBe;
			}
        }
	}
}
