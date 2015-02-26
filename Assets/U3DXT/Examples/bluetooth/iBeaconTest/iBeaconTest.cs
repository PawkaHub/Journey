using System;
using System.Collections;
using UnityEngine;
using U3DXT.Core;
using U3DXT.iOS.Native.Foundation;
using U3DXT.iOS.Native.CoreLocation;
using U3DXT.iOS.Native.CoreBluetooth;
using System.Collections.Generic;

public class iBeaconTest : MonoBehaviour {
	
	private const string proximityUUID = "39ED98FF-2900-441A-802F-9C398FC199D2";
	private const string beaconID = "com.vitapoly.myregion";
	
	// common
	private CLBeaconRegion beaconRegion;
	
	// for advertising
	private CBPeripheralManager peripheralManager;
	private PeripheralManagerDelegate pmDelegate;
	
	// for monitoring
	private CLLocationManager locationManager;
	private bool isMonitoring = false;
	
	void Start() {
		if (CoreXT.IsDevice) {
			// subscribes to events
			
			beaconRegion = new CLBeaconRegion(new NSUUID(proximityUUID), beaconID);
			
			// only works if Unity is in the background.  But if Unity is killed, it doesn't work.
			beaconRegion.notifyEntryStateOnDisplay = true;
			beaconRegion.notifyOnEntry = true;
			beaconRegion.notifyOnExit = true;
		}
	}
	
	void OnDestroy() {
		if (CoreXT.IsDevice) {
			// unsubscribe to events
			
			StopAdvertise();
			StopMonitor();
		}
	}
	
	public class PeripheralManagerDelegate : CBPeripheralManagerDelegate {
		private iBeaconTest parent;
		public PeripheralManagerDelegate(iBeaconTest parent) {
			this.parent = parent;
		}
		
		public override void DidUpdateState(CBPeripheralManager peripheral) {
			parent.PeripheralManagerDidUpdateState();
		}
	}
	
	void Advertise() {
		// don't advertise and monitor at the same time
		StopMonitor();
		
		if ((peripheralManager != null) && (peripheralManager.isAdvertising)) {
			Log("Already advertising.");
			return;
		}
		
		pmDelegate = new PeripheralManagerDelegate(this);
		peripheralManager = new CBPeripheralManager(pmDelegate, null);
	}
				
	internal void PeripheralManagerDidUpdateState() {
		if (peripheralManager.state == CBPeripheralManagerState.PoweredOn) {
			Log("Powered ON.");
			Dictionary<object, object> data = beaconRegion.PeripheralData(-50);
			peripheralManager.StartAdvertising(data);
			Log("Advertising.");
		} else if (peripheralManager.state == CBPeripheralManagerState.PoweredOff) {
			Log("Powered OFF.");
			peripheralManager.StopAdvertising();
		}
	}
	
	void StopAdvertise() {
		if ((peripheralManager != null) && (peripheralManager.isAdvertising)) {
			peripheralManager.StopAdvertising();
			Log("Stopped advertising.");
		}
	}
	
	void Monitor() {
		// don't advertise and monitor at the same time
		StopAdvertise();
		
		if (isMonitoring) {
			Log("Already monitoring.");
			return;
		}
		
		if (locationManager == null) {
			locationManager = new CLLocationManager();
		}

		locationManager.DidStartMonitoring += DidStartMonitoringForRegion;
		locationManager.DidEnter += DidEnterRegion;
		locationManager.DidExit += DidExitRegion;
		locationManager.DidRangeBeacons += DidRangeBeacons;
		locationManager.StartMonitoring(beaconRegion);
		locationManager.StartRangingBeaconsInRegion(beaconRegion);
		
		Log("Monitoring.");
		isMonitoring = true;
	}
	
	void DidStartMonitoringForRegion(object sender, CLLocationManager.DidStartMonitoringEventArgs e) {
		locationManager.StartRangingBeaconsInRegion(beaconRegion);
	}
	
	void DidEnterRegion(object sender, CLLocationManager.DidEnterEventArgs e) {
		var notif = new LocalNotification();
		notif.alertBody = "Found beacon!";
		NotificationServices.PresentLocalNotificationNow(notif);

		Log("Found beacon.");
		locationManager.StartRangingBeaconsInRegion(beaconRegion);
	}
	
	void DidExitRegion(object sender, CLLocationManager.DidExitEventArgs e) {
		var notif = new LocalNotification();
		notif.alertBody = "Lost beacon!";
		NotificationServices.PresentLocalNotificationNow(notif);

		Log("Left region.");
		locationManager.StopRangingBeaconsInRegion(beaconRegion);
	}
	
	void DidRangeBeacons(object sender, CLLocationManager.DidRangeBeaconsEventArgs e) {
		if (e.beacons.Length == 0)
			return;
		
		CLBeacon beacon = e.beacons[0] as CLBeacon;
		Log("distance: " + beacon.proximity);
	}
	
	void StopMonitor() {
		
		if (!isMonitoring)
			return;
		
		locationManager.StopRangingBeaconsInRegion(beaconRegion);
		locationManager.StopMonitoring(beaconRegion);
		locationManager.DidStartMonitoring -= DidStartMonitoringForRegion;
		locationManager.DidEnter -= DidEnterRegion;
		locationManager.DidExit -= DidExitRegion;
		locationManager.DidRangeBeacons -= DidRangeBeacons;

		Log("Stopped monitoring.");
		isMonitoring = false;
	}
	
	void OnGUI() {
		
		KitchenSink.OnGUIBack();
		
		if (CoreXT.IsDevice) {
			
			GUILayout.BeginArea(new Rect(50, 50, Screen.width - 100, Screen.height/2 - 50));
			GUILayout.BeginHorizontal();

			if (GUILayout.Button("Advertise Beacon", GUILayout.ExpandHeight(true))) {
				Advertise();
			}
			
			if (GUILayout.Button("Stop Advertising", GUILayout.ExpandHeight(true))) {
				StopAdvertise();
			}
			
			if (GUILayout.Button("Monitor Beacon", GUILayout.ExpandHeight(true))) {
				Monitor();
			}
			
			if (GUILayout.Button("Stop Monitoring", GUILayout.ExpandHeight(true))) {
				StopMonitor();
			}
			
			GUILayout.EndHorizontal();
			GUILayout.EndArea();
		}
		
		OnGUILog();
	}


#region Debug logging
	string _log = "Debug log:";
	Vector2 _scrollPosition = Vector2.zero;
	
	void OnGUILog() {
		GUILayout.BeginArea(new Rect(50, Screen.height / 2, Screen.width - 100, Screen.height / 2 - 50));
		_scrollPosition = GUILayout.BeginScrollView(_scrollPosition);
		GUI.skin.box.wordWrap = true;
		GUI.skin.box.alignment = TextAnchor.UpperLeft;
		GUILayout.Box(_log, GUILayout.ExpandHeight(true));
		GUILayout.EndScrollView();
		GUILayout.EndArea();
	}
	
	void Log(string str) {
		_log += "\n" + str;
		_scrollPosition.y = Mathf.Infinity;
	}
#endregion
}
