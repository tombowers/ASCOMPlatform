//tabs=4
// --------------------------------------------------------------------------------
//
// ASCOM Dome driver for DeviceHub
//
// Description:	This class implements the Dome object that is exposed by the
//				ASCOM Device Hub application. The properties get values that are
//				cached by the Device Hub Dome Manager and set values that update the
//				cache and forward them to the true driver. Dome command methods
//				make calls through the Dome Manager
//
// Implements:	ASCOM Dome interface version: 2
// Author:		(RDB) Rick Burke <astroman133@gmail.com>
//
// Edit Log:
//
// Date			Who	Vers	Description
// -----------	---	-----	-------------------------------------------------------
// 11-Dec-2018	RDB	6.4.0	Initial edit, created from ASCOM driver template
// --------------------------------------------------------------------------------
//

using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using ASCOM.DeviceInterface;
using ASCOM.Utilities;

using static System.FormattableString;

namespace ASCOM.DeviceHub
{
	// Your driver's DeviceID is ASCOM.DeviceHub.Dome

	// The Guid attribute sets the CLSID for ASCOM.DeviceHub.Dome
	// The ClassInterface/None attribute prevents an empty interface called
	// _DeviceHub from being created and used as the [default] interface

	/// <summary>
	/// ASCOM Dome Driver for DeviceHub.
	/// </summary>
	[ComVisible( true )]
	[Guid( "3c3cac40-ad1a-4453-a404-22b9ef331a3b" )]
	[ProgId( "ASCOM.DeviceHub.Dome" )]
	[ServedClassName( "Device Hub Dome" )]
	[ClassInterface( ClassInterfaceType.None )]
	public class Dome : DeviceDriverBase, IDomeV2
	{
		#region Private Fields and Properties

		private DomeManager DomeManager => DomeManager.Instance;

		/// <summary>
		/// Returns true if there is a valid connection to the driver hardware
		/// </summary>
		private bool ConnectedState { get; set; }

		private Util Utilities { get; set; }

		#endregion Private Fields and Properties

		#region Instance Constructor 

		/// <summary>
		/// Initializes a new instance of the <see cref="DeviceHub"/> class.
		/// Must be public for COM registration.
		/// </summary>
		public Dome()
		{
			_driverID = Marshal.GenerateProgIdForType( this.GetType() );
			_driverDescription = GetDriverDescriptionFromAttribute();

			_logger = new TraceLogger( "", "DeviceHubDome" );
			ReadProfile(); // Read device configuration from the ASCOM Profile store

			_logger.LogMessage( "Dome", "Starting initialization" );

			ConnectedState = DomeManager.IsConnected;
			Utilities = new Util(); //Initialize util object

			// We need this to get the dome ID.

			AppSettingsManager.LoadAppSettings();
			_logger.LogMessage( "Dome", "Completed initialization" );
		}

		#endregion Instance Constructor

		//
		// PUBLIC COM INTERFACE IDomeV2 IMPLEMENTATION
		//

		#region Common properties and methods.

		/// <summary>
		/// Displays the Setup Dialog form.
		/// If the user clicks the OK button to dismiss the form, then
		/// the new settings are saved, otherwise the old values are reloaded.
		/// THIS IS THE ONLY PLACE WHERE SHOWING USER INTERFACE IS ALLOWED!
		/// </summary>
		public void SetupDialog()
		{
			if ( Server.DomesInUse > 1 || DomeManager.Instance.IsConnected )
			{
				System.Windows.MessageBox.Show( "Unable to change Dome Properties at this time." );

				return;
			}

			if ( ConnectedState )
			{
				System.Windows.MessageBox.Show( "Already connected, just press OK" );

				return;
			}

			// Launch the setup dialog on the U/I thread.

			Task taskShow = new Task( () =>
			{
				DomeDriverSetupDialogViewModel vm = new DomeDriverSetupDialogViewModel();
				DomeDriverSetupDialogView view = new DomeDriverSetupDialogView { DataContext = vm };
				vm.RequestClose += view.OnRequestClose;
				vm.InitializeCurrentDome( DomeManager.DomeID, DomeManager.Instance.FastPollingPeriod );
				vm.IsLoggingEnabled = _logger.Enabled;
				view.ContentRendered += ( sender, eventArgs ) => view.Activate();

				bool? result = view.ShowDialog();

				if ( result.HasValue && result.Value )
				{
					_logger.Enabled = vm.IsLoggingEnabled;
					DomeManager.DomeID = vm.DomeSetupVm.DomeID;
					DomeManager.Instance.SetFastUpdatePeriod( vm.DomeSetupVm.FastUpdatePeriod );

					SaveProfile();
					UpdateAppProfile( DomeManager.DomeID );

					view.DataContext = null;
					view = null;

					vm.Dispose();
					vm = null;
				}
			} );

			taskShow.Start( Globals.UISyncContext );

			// Wait for the task to be completed and the profile to be updated.

			taskShow.Wait();
		}

		public ArrayList SupportedActions
		{
			get
			{
				ArrayList retval;
				string msg = "";

				try
				{
					retval = DomeManager.SupportedActions;
					msg += $"Returning list from driver {_done}";
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( "Get SupportedActions:", msg );
				}

				return retval;
			}
		}

		public string Action( string actionName, string actionParameters )
		{
			string retval;

			if ( String.IsNullOrEmpty( actionName ) )
			{
				throw new InvalidValueException( "Action method: no actionName was provided." );
			}

			string msg = $"Action {actionName}, parameters {actionParameters}";


			try
			{
				retval = DomeManager.Action( actionName, actionParameters );
				msg += $", returned {retval}{_done}";
			}
			catch ( Exception )
			{
				msg += _failed;

				throw;
			}
			finally
			{
				LogMessage( "Action:", msg );
			}

			return retval;
		}

		public void CommandBlind( string command, bool raw )
		{
			string msg = $"Command {command}, Raw {raw}";

			try
			{
				DomeManager.CommandBlind( command, raw );
				msg += _done;
			}
			catch ( Exception )
			{
				msg += _failed;

				throw;
			}
			finally
			{
				LogMessage( "CommandBlind:", msg );
			}
		}

		public bool CommandBool( string command, bool raw )
		{
			bool retval;
			string msg = $"Command {command}, Raw {raw}";

			try
			{
				retval = DomeManager.CommandBool( command, raw );
				msg += $", Returned {retval}{_done}.";
			}
			catch ( Exception )
			{
				msg += _failed;

				throw;
			}
			finally
			{
				LogMessage( "CommandBool:", msg );
			}

			return retval;
		}

		public string CommandString( string command, bool raw )
		{
			string retval;
			string msg = $"Command {command}, Raw {raw}";

			try
			{
				retval = DomeManager.CommandString( command, raw );
				msg += $", Returned {retval}{_done}.";
			}
			catch ( Exception )
			{
				msg += _failed;

				throw;
			}
			finally
			{
				LogMessage( "CommandString:", msg );
			}

			return retval;
		}

		public void Dispose()
		{
			// Clean up the trace logger and utilities objects

			_logger.Enabled = false;
			_logger.Dispose();
			_logger = null;
			Utilities.Dispose();
			Utilities = null;
		}

		public bool Connected
		{
			get
			{
				LogMessage( "Get Connected: ", ConnectedState.ToString() );

				return ConnectedState;
			}
			set
			{
				string msg = $"Setting Connected to {value}";

				try
				{
					if ( value == ConnectedState ) // Short circuit since we are already connected.
					{
						msg += " (no change)";

						return;
					}

					if ( value )
					{
						msg += $" (connecting to {DomeManager.DomeID})";

						// Do this on the U/I thread.

						Task task = new Task(() =>
						{
							ConnectedState = DomeManager.Connect();
						});

						task.Start(Globals.UISyncContext);
						task.Wait();
					}
					else
					{
						ConnectedState = false;
						Server.DisconnectDomeIf();
						msg += $" (disconnecting from {DomeManager.DomeID}){_done}";
					}
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( "Set Connected:", msg );
				}
			}
		}

		public string Description
		{
			get
			{
				LogMessage( "Description Get", _driverDescription );

				return _driverDescription;
			}
		}

		public string DriverInfo
		{
			get
			{
				Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
				string driverInfo = Invariant( $"DeviceHub dome driver. Version: {version.Major}.{version.Minor}" );

				LogMessage( "Get DriverInfo:", driverInfo );

				return driverInfo;
			}
		}

		public string DriverVersion
		{
			get
			{
				Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
				string driverVersion = Invariant( $"{version.Major}.{version.Minor}" );

				LogMessage( "Get DriverVersion:", driverVersion );

				return driverVersion;
			}
		}

		public short InterfaceVersion
		{
			// Set by the driver wizard

			get
			{
				short version = 2;

				LogMessage( "Get InterfaceVersion:", version.ToString() );

				return version;
			}
		}

		public string Name
		{
			get
			{
				Assembly assembly = Assembly.GetEntryAssembly();
				var attribute = (AssemblyProductAttribute)assembly.GetCustomAttributes( typeof(AssemblyProductAttribute), false ).FirstOrDefault();
				string name = attribute.Product;

				string downstreamName = DomeManager.Name;

				if ( !String.IsNullOrEmpty( downstreamName ) )
				{
					name += $" -> {downstreamName}";
				}

				LogMessage( "Get Name", name );

				return name;
			}
		}

		#endregion

		#region IDome Implementation

		public void AbortSlew()
		{
			string name = "AbortSlew:";

			CheckConnected( name );

			string msg = "";

			try
			{
				DomeManager.StopDomeMotion();

				msg = _done;
			}
			catch ( Exception )
			{
				msg = _failed;

				throw;
			}
			finally
			{
				LogMessage( name, msg );
			}
		}

		public double Altitude
		{
			get
			{
				string name = "Get Altitude:";

				CheckConnected( name );

				var retval = Double.NaN;
				string msg = "";

				try
				{
					Exception xcp = DomeManager.Status.GetException();

					if ( xcp != null )
					{
						throw xcp;
					}

					retval = DomeManager.Status.Altitude;
					msg += $"{Utilities.DegreesToDMS( retval )}{_done}";
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( name, msg );
				}

				return retval;
			}
		}

		public bool AtHome
		{
			get
			{
				string name = "Get AtHome:";
				CheckConnected( name );

				var retval = false;
				string msg = "";

				try
				{
					Exception xcp = DomeManager.Status.GetException();

					if ( xcp != null )
					{
						throw xcp;
					}

					retval = DomeManager.Status.AtHome;
					msg += $"{retval}{_done}";
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( name, msg );
				}

				return retval;
			}
		}

		public bool AtPark
		{
			get
			{
				string name = "Get AtPark:";
				CheckConnected( name );

				var atPark = false;
				string msg = "";

				try
				{
					Exception xcp = DomeManager.Status.GetException();

					if ( xcp != null )
					{
						throw xcp;
					}

					atPark = DomeManager.Status.AtPark;
					msg += $"{atPark}{_done}";
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( name, msg );
				}

				return atPark;
			}
		}

		public double Azimuth
		{
			get
			{
				string name = "Get Azimuth:";
				CheckConnected( name );

				var retval = Double.NaN;
				string msg = "";

				try
				{
					Exception xcp = DomeManager.Status.GetException();

					if ( xcp != null )
					{
						throw xcp;
					}

					retval = DomeManager.Status.Azimuth;
					msg += $"{Utilities.DegreesToDMS( retval )}{ _done}";

				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( name, msg );
				}

				return retval;
			}
		}

		public bool CanFindHome
		{
			get
			{
				var retval = false;
				string msg = "";

				try
				{
					Exception xcp = DomeManager.Capabilities.GetException();

					if ( xcp != null )
					{
						throw xcp;
					}

					retval = DomeManager.Capabilities.CanFindHome;
					msg += $"{retval}{_done}";
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( "Get CanFindHome:", msg );
				}

				return retval;
			}
		}

		public bool CanPark
		{
			get
			{
				var retval = false;
				string msg = "";

				try
				{
					Exception xcp = DomeManager.Capabilities.GetException();

					if ( xcp != null )
					{
						throw xcp;
					}

					retval = DomeManager.Capabilities.CanPark;
					msg += $", Returned {retval}{_done}.";
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( "Get CanPark:", msg );
				}

				return retval;
			}
		}

		public bool CanSetAltitude
		{
			get
			{
				var retval = false;
				string msg = "";

				try
				{
					Exception xcp = DomeManager.Capabilities.GetException();

					if ( xcp != null )
					{
						throw xcp;
					}

					retval = DomeManager.Capabilities.CanSetAltitude;
					msg += $"{retval}{_done}";
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( "Get CanSetAltitude:", msg );
				}

				return retval;
			}
		}

		public bool CanSetAzimuth
		{
			get
			{
				var retval = false;
				string msg = "";

				try
				{
					Exception xcp = DomeManager.Capabilities.GetException();

					if ( xcp != null )
					{
						throw xcp;
					}

					retval = DomeManager.Capabilities.CanSetAzimuth;
					msg += $"{retval}{_done}";
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( "Get CanSetAzimuth:", msg );
				}

				return retval;
			}
		}

		public bool CanSetPark
		{
			get
			{
				var retval = false;
				string msg = "";

				try
				{
					Exception xcp = DomeManager.Capabilities.GetException();

					if ( xcp != null )
					{
						throw xcp;
					}

					retval = DomeManager.Capabilities.CanSetPark;
					msg += $"{retval}{_done}";
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( "Get CanSetPark:", msg );
				}

				return retval;
			}
		}

		public bool CanSetShutter
		{
			get
			{
				var retval = false;
				string msg = "";

				try
				{
					Exception xcp = DomeManager.Capabilities.GetException();

					if ( xcp != null )
					{
						throw xcp;
					}

					retval = DomeManager.Capabilities.CanSetShutter;
					msg += $"{retval}{_done}";
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( "Get CanSetShutter:", msg );
				}

				return retval;
			}
		}

		public bool CanSlave
		{
			get
			{
				bool retval = true;

				LogMessage( "Get CanSlave:", retval.ToString() );

				return retval;
			}
		}

		public bool CanSyncAzimuth
		{
			get
			{
				var retval = false;
				string msg = "";

				try
				{
					Exception xcp = DomeManager.Capabilities.GetException();

					if ( xcp != null )
					{
						throw xcp;
					}

					retval = DomeManager.Capabilities.CanSyncAzimuth;
					msg += $"{retval}{_done}";
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( "Get CanSyncAzimuth:", msg );
				}

				return retval;
			}
		}

		public void CloseShutter()
		{
			string name = "CloseShutter:";

			CheckConnected( name );
			CheckCapabilityForMethod( name, "CanSetShutter", DomeManager.Capabilities.CanSetShutter );

			string msg = "";
			try
			{
				DomeManager.CloseDomeShutter();
				msg += _done;
			}
			catch ( Exception )
			{
				msg += _failed;

				throw;
			}
			finally
			{
				LogMessage( name, msg );
			}
		}

		public void FindHome()
		{
			string name = "FindHome:";

			CheckConnected( name );
			CheckCapabilityForMethod( name, "CanFindHome", DomeManager.Capabilities.CanFindHome );

			string msg = "";
			try
			{
				DomeManager.FindHome();
				msg += _done;
			}
			catch ( Exception )
			{
				msg += _failed;

				throw;
			}
			finally
			{
				LogMessage( name, msg );
			}
		}

		public void OpenShutter()
		{
			string name = "OpenShutter:";

			CheckConnected( name );
			CheckCapabilityForMethod( name, "CanSetShutter", DomeManager.Capabilities.CanSetShutter );

			string msg = "";

			try
			{
				DomeManager.OpenDomeShutter();
				msg += _done;
			}
			catch ( Exception )
			{
				msg += _failed;

				throw;
			}
			finally
			{
				LogMessage( name, msg );
			}
		}

		public void Park()
		{
			string name = "Park:";

			CheckConnected( name );
			CheckCapabilityForMethod( name, "CanPark", DomeManager.Capabilities.CanPark );

			if ( DomeManager.Status.AtPark )
			{
				return;
			}

			string msg = "";

			try
			{
				DomeManager.ParkTheDome();
				msg += _done;
			}
			catch ( Exception )
			{
				msg += _failed;

				throw;
			}
			finally
			{
				LogMessage( name, msg );
			}
		}

		public void SetPark()
		{
			string name = "SetPark:";

			CheckConnected( name );
			CheckCapabilityForMethod( name, "CanSetPark", DomeManager.Capabilities.CanSetPark );

			string msg = "";

			try
			{
				DomeManager.SetPark();
				msg += _done;
			}
			catch ( Exception )
			{
				msg += _failed;

				throw;
			}
			finally
			{
				LogMessage( "SetPark:", msg );
			}
		}

		public ShutterState ShutterStatus
		{
			get
			{
				string name = "Get ShutterStatus:";

				CheckConnected( name );
				CheckCapabilityForMethod( name, "CanSetShutter", DomeManager.Capabilities.CanSetShutter );

				ShutterState retval;
				string msg = "";

				try
				{
					Exception xcp = DomeManager.Status.GetException();

					if ( xcp != null )
					{
						throw xcp;
					}

					retval = DomeManager.Status.ShutterStatus;
					msg += $"{retval}{_done}";
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( name, msg );
				}

				return retval;
			}
		}

		public bool Slaved
		{
			get
			{
				string name = "Get Slaved:";

				CheckConnected( name );

				var retval = false;
				string msg = "";

				try
				{
					Exception xcp = DomeManager.Status.GetException();

					if ( xcp != null )
					{
						throw xcp;
					}

					retval = Globals.IsDomeSlaved;
					msg += $"{retval}{_done}";
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( name, msg );
				}

				return retval;
			}
			set
			{
				string name = "Set Slaved:";

				CheckConnected( name );
				CheckCapabilityForProperty( name, "CanSetAzimuth", DomeManager.Capabilities.CanSetAzimuth );

				string msg = $"Setting Slaved to {value}";

				try
				{
					if ( !DomeManager.IsScopeReadyToSlave )
					{
						throw new PropertyNotImplementedException( 
							"Unable to slave the dome because no telescope is connected or no position is available!" );
					}

					if ( value == Globals.IsDomeSlaved ) // Short circuit since we are already slaved correctly.
					{
						msg += " (no change)";

						return;
					}

					if ( !ConnectedState )
					{
						throw new NotConnectedException();
					}

					if ( value )
					{
						msg += $"(Enable slaving for {DomeManager.DomeID}){_done}";
					}
					else
					{
						msg += $"(Disable slaving for {DomeManager.DomeID}){_done}";
					}

					DomeManager.SetSlavedState( value );
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( name, msg );
				}
			}
		}

		public void SlewToAltitude( double altitude )
		{
			string name = "SlewToAltitude:";

			CheckConnected( name );
			CheckCapabilityForMethod( name, "CanSetAltitude", DomeManager.Capabilities.CanSetAltitude );

			string msg = $"Altitude = {altitude:F5}";

			try
			{
				DomeManager.SlewDomeShutter( altitude );
				msg += _done;
			}
			catch ( Exception )
			{
				msg += _failed;

				throw;
			}
			finally
			{
				LogMessage( name, msg );
			}
		}

		public void SlewToAzimuth( double azimuth )
		{
			string name = "SlewToAzimuth:";

			CheckConnected( name );
			CheckCapabilityForMethod( name, "CanSetAzimuth", DomeManager.Capabilities.CanSetAzimuth );

			string msg = $"Azimuth = {azimuth:F5}";

			try
			{
				DomeManager.SlewDomeToAzimuth( azimuth );
				msg += _done;
			}
			catch ( Exception )
			{
				msg += _failed;

				throw;
			}
			finally
			{
				LogMessage( name, msg );
			}
		}

		public bool Slewing
		{
			get
			{
				var retval = false;
				string msg = "";

				try
				{
					Exception xcp = DomeManager.Status.GetException();

					if ( xcp != null )
					{
						throw xcp;
					}

					retval = DomeManager.Status.Slewing;
					msg += $"{retval}{_done}";
				}
				catch ( Exception )
				{
					msg += _failed;

					throw;
				}
				finally
				{
					LogMessage( "Get Slewing:", msg );
				}

				return retval;
			}
		}

		public void SyncToAzimuth( double azimuth )
		{
			string name = "SyncToAzimuth:";
			CheckCapabilityForMethod( name, "CanSyncAzimuth", DomeManager.Capabilities.CanSyncAzimuth );
			CheckRange( name, azimuth, 0.0, 360.0 );

			string msg = $"Azimuth = {azimuth:f5}";

			try
			{
				DomeManager.SyncToAzimuth( azimuth );
				msg += _done;
			}
			catch ( Exception )
			{
				msg += _failed;

				throw;
			}
			finally
			{
				LogMessage( name, msg );
			}
		}

		#endregion

		#region Private properties and methods
		
		private void CheckConnected( string identifier )
		{
			CheckConnected( identifier, ConnectedState );
		}

		/// <summary>
		/// Helper method to read the driver description from the custom attribute.
		/// </summary>
		/// <returns>the display name for the class</returns>
		private string GetDriverDescriptionFromAttribute()
		{
			string retval = "Unknown driver";

			Type t = this.GetType(); ;
			Type attributeType = typeof( ServedClassNameAttribute );
			object[] customAtts = t.GetCustomAttributes( attributeType, false );

			if ( customAtts != null && customAtts.Length == 1 )
			{
				if ( customAtts[0] is ServedClassNameAttribute attr )
				{
					retval = attr.DisplayName;
				}
			}

			return retval;
		}

		/// <summary>
		/// Read the device configuration from the ASCOM Profile store
		/// </summary>
		internal void ReadProfile()
		{
			DomeSettings settings = DomeSettings.FromProfile();
			Globals.DomeAzimuthAdjustment = settings.AzimuthAdjustment;
			Globals.UsePOTHDomeSlaveCalculation = settings.UsePOTHDomeSlaveCalculation;
			_logger.Enabled = settings.IsLoggingEnabled;
		}

		/// <summary>
		/// Write the device configuration to the  ASCOM  Profile store
		/// </summary>
		internal void SaveProfile()
		{
			DomeSettings settings = new DomeSettings
			{
				DomeID = DomeManager.DomeID,
				DomeLayout = Globals.DomeLayout,
				AzimuthAdjustment = Globals.DomeAzimuthAdjustment,
				IsLoggingEnabled = _logger.Enabled,
				FastUpdatePeriod = DomeManager.FastPollingPeriod
			};

			settings.ToProfile();
		}

		internal void UpdateAppProfile( string domeID )
		{
			string deviceID = Globals.DevHubDomeID;

			using ( Profile appProfile = new Profile() )
			{
				appProfile.DeviceType = "Dome";

				appProfile.WriteValue( deviceID, "DomeID", domeID );
			}
		}

		#endregion
	}
}
