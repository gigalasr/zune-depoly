namespace ZuneDeploy.Transport;

public enum DeviceFamily : byte {
    Keel = 0,      // 1st Gen - Zune 30
    Scorpius = 2,  // 2nd Gen Flash - Zune 4/8/16
    Draco = 3,     // 2nd Gen HDD - Zune 80/120
    Pavo = 6       // Zune HD 16/32/64
}

public static class DeviceFamilyExtensions {
    extension(DeviceFamily family) {
        public string AsWellKnownName() {
            return family switch {
                DeviceFamily.Keel => "Keel (Zune 30)",
                DeviceFamily.Scorpius => "Scorpius (Zune 4/8/16)",
                DeviceFamily.Draco => "Draco (Zune 80/120)",
                DeviceFamily.Pavo => "Pavo (Zune HD)",
                _ => "Unknown"
            };
        }

        public bool IsZuneHD() {
            return family == DeviceFamily.Pavo;
        }

        public static DeviceFamily FromByte(byte b) {
            return (DeviceFamily)b;
        }
    }
}
