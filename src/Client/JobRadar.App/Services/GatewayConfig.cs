namespace JobRadar.App.Services;

public static class GatewayConfig
{
    // Android emulator can't resolve "localhost" as the host machine - 10.0.2.2 is the special
    // alias the AVD provides for that. iOS simulator and Windows/Mac Catalyst share the host's
    // network stack, so plain localhost works for them. A physical device needs your machine's
    // real LAN IP here instead (e.g. http://192.168.1.42:8080/) since it isn't the same host.
    public static string BaseUrl =>
        DeviceInfo.Platform == DevicePlatform.Android
            ? "http://10.0.2.2:8080/"
            : "http://localhost:8080/";
}
