namespace ShrimpleRagdolls.Editor;

[CustomEditor( typeof( ShrimpleRagdoll.BoneList ) )]
internal class BoneListWidget : ControlWidget
{
	public override bool IsControlButton => true;
	public override bool IsControlHovered => base.IsControlHovered || _menu.IsValid();

	Menu _menu;

	public BoneListWidget( SerializedProperty property ) : base( property )
	{
		Cursor = CursorShape.Finger;

		Layout = Layout.Row();
		Layout.Spacing = 2;
	}

	protected override void PaintControl()
	{
		var value = SerializedProperty.GetValue<ShrimpleRagdoll.BoneList>();

		var color = IsControlHovered ? Theme.Blue : Theme.TextControl;
		var rect = LocalRect;

		rect = rect.Shrink( 8, 0 );

		Paint.SetPen( color );
		Paint.DrawText( rect, value.Selected ?? "None", TextFlag.LeftCenter );

		Paint.SetPen( color );
		Paint.DrawIcon( rect, "Arrow_Drop_Down", 17, TextFlag.RightCenter );
	}

	protected override void OnMousePress( MouseEvent e )
	{
		base.OnMousePress( e );

		if ( e.LeftMouseButton && !_menu.IsValid() )
		{
			OpenMenu();
		}
	}

	void OpenMenu()
	{
		_menu = new Menu();
		_menu.DeleteOnClose = true;

		var options = SerializedProperty.GetValue<ShrimpleRagdoll.BoneList>().Options;

		_menu.AddLineEdit( "Filter",
			placeholder: "Search",
			autoFocus: true,
			onChange: s => PopulateMenu( _menu, options.Select( x => $"{options.IndexOf( x ).ToString( "D2" )}. {x}" ), s ) );

		_menu.AboutToShow += () => PopulateMenu( _menu, options.Select( x => $"{options.IndexOf( x ).ToString( "D2" )}. {x}" ) );

		_menu.OpenAtCursor( true );
		_menu.MinimumWidth = ScreenRect.Width;
	}

	private void PopulateMenu( Menu menu, IEnumerable<string> items, string filter = null )
	{
		menu.RemoveMenus();
		menu.RemoveOptions();

		foreach ( var widget in menu.Widgets.Skip( 1 ) )
		{
			menu.RemoveWidget( widget );
		}

		const int maxFiltered = 10;

		var useFilter = !string.IsNullOrEmpty( filter );
		var truncated = 0;

		if ( useFilter )
		{
			var filtered = items.Where( x => x != null && x.Contains( filter, StringComparison.OrdinalIgnoreCase ) ).ToArray();

			if ( filtered.Length > maxFiltered + 1 )
			{
				truncated = filtered.Length - maxFiltered;
				items = filtered.Take( maxFiltered );
			}
			else
			{
				items = filtered;
			}
		}

		menu.AddOptions( items, x => x, x =>
		{
			if ( x != null )
			{
				SerializedProperty.GetValue<ShrimpleRagdoll.BoneList>().Selected = x.Split( ". " )[1];
				SignalValuesChanged();
			}
		}, flat: useFilter );

		if ( truncated > 0 )
		{
			menu.AddOption( $"...and {truncated} more" );
		}

		menu.AdjustSize();
		menu.Update();
	}
}
