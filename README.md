# BloubulLE - A Bluetooth LE plugin for Xamarin  

This library is a hard fork of https://github.com/xabre/xamarin-bluetooth-le (Plugin.BLE). We removed MvvmCross dependency to use the DependencyService instead, removed the samples, merged the UWP branch, restructured and renamed the library. If you want to contribute, please contribute to the original library at https://github.com/xabre/xamarin-bluetooth-le.
The library name is Afrikaans and the fan name of the South-African Rugby team Blue Bulls (Blou Bulle). The "LE" is capitalized for Low Energy (Bluetooth).

## Support & Limitations

| Platform  | Version | Limitations |
| ------------- | ----------- | ----------- |
| Xamarin.Android | 5.0 |  |
| Xamarin.iOS     | 8.0 |  |
| Xamarin.UWP     | 16299 |  |

## Installation

**Android**

Add these permissions to AndroidManifest.xml. For Marshmallow and above, please follow [Requesting Runtime Permissions in Android Marshmallow](https://blog.xamarin.com/requesting-runtime-permissions-in-android-marshmallow/) and don't forget to prompt the user for the location permission.

```xml
<uses-permission android:name="android.permission.ACCESS_COARSE_LOCATION" />
<uses-permission android:name="android.permission.BLUETOOTH" />
<uses-permission android:name="android.permission.BLUETOOTH_ADMIN" />
```

Add this line to your manifest if you want to declare that your app is available to BLE-capable devices **only**:
```xml
<uses-feature android:name="android.hardware.bluetooth_le" android:required="true"/>
````

**UWP**

Add this line to the Package Manifest (.appxmanifest):

```xml
<DeviceCapability Name="bluetooth" />
```

### IBluetoothLE
#### Get the bluetooth status
```csharp
var state = ble.State;
```
You can also listen for State changes. So you can react if the user turns on/off bluetooth on you smartphone.
```csharp
ble.StateChanged += (s, e) => 
{
    Debug.WriteLine($"The bluetooth state changed to {e.NewState}");
};
```

### IAdapter
#### Scan for devices
```csharp
adapter.DeviceDiscovered += (s,a) => deviceList.Add(a.Device);
await adapter.StartScanningForDevicesAsync();
```

##### ScanTimeout
Set `adapter.ScanTimeout` to specify the maximum duration of the scan.

##### ScanMode
Set `adapter.ScanMode` to specify scan mode. It must be set **before** calling `StartScanningForDevicesAsync()`. Changing it while scanning, will not affect the current scan.

#### Connect to device
`ConnectToDeviceAsync` returns a Task that finishes if the device has been connected successful. Otherwise a `DeviceConnectionException` gets thrown.

```csharp
try 
{
    await _adapter.ConnectToDeviceAsync(device);
}
catch(DeviceConnectionException e)
{
    // ... could not connect to device
}
```

#### Connect to known Device
`ConnectToKnownDeviceAsync` can connect to a device with a given GUID. This means that if the device GUID is known, no scan is necessary to connect to a device. This can be very useful for a fast background reconnect.
Always use a cancellation token with this method. 
- On **iOS** it will attempt to connect indefinitely, even if out of range, so the only way to cancel it is with the token.
- On **Android** this will throw a GATT ERROR in a couple of seconds if the device is out of range.

```csharp
try 
{
    await _adapter.ConnectToKnownDeviceAsync(guid, cancellationToken);
}
catch(DeviceConnectionException e)
{
    // ... could not connect to device
}
```

#### Get services
```csharp
var services = await connectedDevice.GetServicesAsync();
```
or get a specific service:
```csharp
var service = await connectedDevice.GetServiceAsync(Guid.Parse("ffe0ecd2-3d16-4f8d-90de-e89e7fc396a5"));
```

#### Get characteristics
```csharp
var characteristics = await service.GetCharacteristicsAsync();
```
or get a specific characteristic:
```csharp
var characteristic = await service.GetCharacteristicAsync(Guid.Parse("d8de624e-140f-4a22-8594-e2216b84a5f2"));
```

#### Read characteristic
```csharp
var bytes = await characteristic.ReadAsync();
```

#### Write characteristic
```csharp
await characteristic.WriteAsync(bytes);
```

#### Characteristic notifications
```csharp
characteristic.ValueUpdated += (o, args) =>
{
    var bytes = args.Characteristic.Value;
};

await characteristic.StartUpdatesAsync();

```

#### Get descriptors
```csharp
var descriptors = await characteristic.GetDescriptorsAsync();
```

#### Read descriptor
```csharp
var bytes = await descriptor.ReadAsync();
```

#### Write descriptor
```csharp
await descriptor.WriteAsync(bytes);
```

#### Get System Devices
        
Returns all BLE devices connected or bonded (only Android) to the system. In order to use the device in the app you have to first call ConnectAsync.
- For iOS the implementation uses get [retrieveConnectedPeripherals(services)](https://developer.apple.com/reference/corebluetooth/cbcentralmanager/1518924-retrieveconnectedperipherals)
- For Android this function merges the functionality of thw following API calls:
    - [getConnectedDevices](https://developer.android.com/reference/android/bluetooth/BluetoothManager.html#getConnectedDevices(int))
    - [getBondedDevices()](https://developer.android.com/reference/android/bluetooth/BluetoothAdapter.html#getBondedDevices()) 

  
```csharp

var systemDevices = adapter.GetSystemConnectedOrPairedDevices();

foreach(var device in systemDevices)
{
    await _adapter.ConnectToDeviceAsync(device); 
}

```
## Caution! Important remarks / API limitations

The BLE API implementation (especially on **Android**) has the following limitations:

- *Characteristic/Descriptor Write*: make sure you call characteristic.**WriteAsync**(...) from the **main thread**, failing to do so will most probably result in a GattWriteError.
- *Sequential calls*: **Always** wait for the previous BLE command to finish before invoking the next. The Android API needs it's calls to be serial, otherwise calls that do not wait for the previous ones will fail with some type of GattError. A more explicit example: if you call this in your view lifecycle (onAppearing etc) all these methods return **void** and 100% don't quarantee that any await bleCommand() called here will be truly awaited by other lifecycle methods.
- *Scan wit services filter*: On **specifically Android 4.3** the *scan services filter does not work* (due to the underlying android implementation). For android 4.3 you will have to use a workaround and scan without a filter and then manually filter by using the advertisement data (which contains the published service GUIDs).

## Best practice

### API
- Surround Async API calls in try-catch blocks. Most BLE calls can/will throw an exception in certain cases, this is especially true for Android. We will try to update the xml doc to reflect this.
```csharp
    try
    {
        await _adapter.ConnectToDeviceAsync(device);
    }
    catch(DeviceConnectionException ex)
    {
        //specific
    }
    catch(Exception ex)
    {
        //generic
    }
```
- **Avoid caching of Characteristic or Service instances between connection sessions**. This includes saving a reference to them in you class between connection sessions etc. After a device has been disconnected all Service & Characteristic instances become **invalid**. Allways **use GetServiceAsync and GetCharacteristicAsync to get a valid instance**.
 
### General BLE iOS, Android

- Scanning: Avoid performing ble device operations like Connect, Read, Write etc while scanning for devices. Scanning is battery-intensive.
    - try to stop scanning before performing device operations (connect/read/write/etc)
    - try to stop scanning as soon as you find the desired device
    - never scan on a loop, and set a time limit on your scan

## Useful Links

- [The original library](https://github.com/xabre/xamarin-bluetooth-le)
- [Android Bluetooth LE guideline](https://developer.android.com/guide/topics/connectivity/bluetooth-le.html)
- [iOS CoreBluetooth Best Practices](https://developer.apple.com/library/ios/documentation/NetworkingInternetWeb/Conceptual/CoreBluetooth_concepts/BestPracticesForInteractingWithARemotePeripheralDevice/BestPracticesForInteractingWithARemotePeripheralDevice.html)
- [MvvmCross](https://github.com/MvvmCross)
- [Monkey Robotics](https://github.com/xamarin/Monkey.Robotics)

## Licence

[Apache 2.0](https://github.com/xabre/MvvmCross-BluetoothLE/blob/master/LICENSE)




