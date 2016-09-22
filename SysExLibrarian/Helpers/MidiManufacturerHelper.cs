namespace SysExLibrarian.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading.Tasks;
    using SysExLibrarian.Extensions;

    public class MidiManufacturerHelper
    {
        /// <summary>
        /// Gets the manufacturer string description from a system-exclusive message file ID
        /// </summary>
        /// <param name="sysExFilePath">The system-exclusive message file path</param>
        /// <remarks>
        /// The first data byte (after the Status 0xF0), should be a Manufacturer's ID.
        /// Since a midi data byte can't be greater than 0x7F, that means there would only be 127 IDs to dole out to manufacturers. Well, there are more than 127 manufacturers of MIDI products.
        /// MMA has assigned particular values of the ID byte to various manufacturers, so that a device can determine whether a SysEx message is intended for it. For example, a Roland device expects an ID byte of 0x41. 
        /// If a Roland device receives a SysEx message whose ID byte isn't 0x41, then the device ignores all of the rest of the bytes up to and including the final 0xF7 which indicates that the SysEx message is finished.
        /// To accomodate a greater range of manufacturer IDs, the MMA decided to reserve a manufacturer ID of 0 for a special purpose. When you see a manufacturer ID of 0, then there will be two more data bytes after this. 
        /// These two data bytes combine to make the real manufacturer ID. So, some manufacturers have IDs that are 3 bytes, where the first byte is always 0. Using this "trick", the range of unique manufacturer IDs is extended to accomodate over 16,000 MIDI manufacturers.
        /// For example, Microsoft's manufacturer ID consists of the 3 bytes 0x00 0x00 0x41. Note that the first byte is 0 to indicate that the real ID is 0x0041, but is still different than Roland's ID which is only the single byte of 0x41.
        /// To accomodate this and differentiate between single-digit manufacture IDs and three digit manufacture IDs, this member will return posivive values for single-digit manufacture IDs and negavive values for all three digit manufacture IDs.
        /// So in the above example Microsoft's ID would be returned as -65, wheres Roland's ID would be returned as 65.
        /// There are also three special manufacturer ID reserved: Educational, RealTime and NonRealTime. The later two relate to universal SysEx messages.
        /// Note: If no manufacturer ID is actually present in the Message data buffer, this member might anyhow return a value, since this property simply evaluates the first data bytes after the initial StatusType.
        /// </remarks>
        /// <returns>A string description of the MIDI manufacturer from the sysex file. Returns "Unknown" if no match is found.</returns>
        public static string GetMidiManufacturerFromSysExFile(string sysExFilePath)
        {
            byte[] bytes = System.IO.File.ReadAllBytes(sysExFilePath);
            byte midiManufacturer = bytes[1];
            if(midiManufacturer == 0x00)
            {   
                var combinedBytes = bytes[1] << 8 | bytes[2] << 16 | bytes[3];
                if (Enum.IsDefined(typeof(MidiManufacturer), (byte)combinedBytes))
                {
                    return ((MidiManufacturer)combinedBytes).DescriptionAttr();
                }
                else
                {
                    return MidiManufacturer.Unknown.DescriptionAttr();
                }
               
            }

            if (Enum.IsDefined(typeof(MidiManufacturer), midiManufacturer))
            {
                return ((MidiManufacturer)midiManufacturer).DescriptionAttr();
            }
            else
            {
                return MidiManufacturer.Unknown.DescriptionAttr();
            }
        }

        /// <summary>
        /// An enumeration for MIDI Manufacturer id numbers. 
        /// </summary>
        /// <remarks>
        /// A list of id numbers can be found at https://www.midi.org/specifications/item/manufacturer-id-numbers
        /// </remarks>
        public enum MidiManufacturer : byte
        {
            [Description("Sequential Circuits")]
            SequentialCircuits = 0x01,
            [Description("Moog")]
            Moog = 0x04,
            [Description("Alesis")]
            Alesis = 0x00E,
            [Description("Clavia Digital Instruments")]
            Clavia = 0x33,
            [Description("Kawai")]
            Kawai = 0x40,
            [Description("Roland")]
            Roland = 0x41,
            [Description("Korg")]
            Korg = 0x42,
            [Description("Yamaha")]
            Yamaha = 0x43,
            [Description("Arturia")]
            Arturia = 0x08B,
            [Description("Unknown")]
            Unknown = 0
        }
    }
}
