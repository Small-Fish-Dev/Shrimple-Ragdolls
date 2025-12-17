namespace ShrimpleRagdolls.Editor;

[CustomEditor( typeof( ShrimpleRagdollModeProperty ) )]
public class ShrimpleRagdollModeControlWidget : ControlWidget
{
	public override bool IsControlActive => base.IsControlActive || _menu.IsValid();
	public override bool IsControlButton => true;
	public override bool IsControlHovered => base.IsControlHovered || _menu.IsValid();

	public override bool SupportsMultiEdit => true;

	protected virtual float? MenuWidthOverride => null;

	private PopupWidget _menu;
	private ShrimpleRagdollModeInfo[] _modes;

	public ShrimpleRagdollModeControlWidget( SerializedProperty property ) : base( property )
	{
		Cursor = CursorShape.Finger;

		Layout = Layout.Row();
		Layout.Spacing = 2;

		// Cache the available modes from the registry
		_modes = ShrimpleRagdollModeRegistry
			.GetRegisteredModes()
			.OrderBy( x => x.Name )
			.ToArray();
	}

	protected override void PaintControl()
	{
		var rect = LocalRect;
		rect = rect.Shrink( 8, 0 );

		var color = IsControlHovered ? Theme.Blue : Theme.TextControl;
		if ( IsControlDisabled )
			color = color.WithAlpha( 0.5f );

		if ( SerializedProperty.IsMultipleDifferentValues )
		{
			Paint.SetPen( Theme.MultipleValues );
			Paint.DrawText( rect, "Multiple Values", TextFlag.LeftCenter );
		}
		else
		{
			// Current value (string name)
			var value = SerializedProperty.GetValue<ShrimpleRagdollModeProperty>( "Disabled" );
			var currentName = value.Name;

			var mode = _modes.FirstOrDefault( x => x.Name == currentName );

			var display = string.IsNullOrEmpty( mode.Name )
				? "Disabled"
				: mode.Name;

			Paint.SetPen( color );
			Paint.DrawText( rect, display, TextFlag.LeftCenter );
		}

		// dropdown arrow
		Paint.SetPen( color );
		Paint.DrawIcon( rect, "Arrow_Drop_Down", 17, TextFlag.RightCenter );
	}

	public override void StartEditing()
	{
		if ( IsControlDisabled )
			return;

		if ( !_menu.IsValid() )
		{
			OpenMenu();
		}
	}

	protected override void OnMouseClick( MouseEvent e )
	{
		if ( IsControlDisabled )
			return;

		if ( e.LeftMouseButton && !_menu.IsValid() )
		{
			OpenMenu();
		}
	}

	protected override void OnDoubleClick( MouseEvent e )
	{
		// nothing
	}

	private void OpenMenu()
	{
		PropertyStartEdit();

		_menu = new PopupWidget( null )
		{
			Layout = Layout.Column()
		};

		var menuWidth = MenuWidthOverride ?? ScreenRect.Width;
		_menu.MinimumWidth = menuWidth;
		_menu.MaximumWidth = menuWidth;
		_menu.OnLostFocus += PropertyFinishEdit;

		var scroller = _menu.Layout.Add( new ScrollArea( this ), 1 );
		scroller.Canvas = new Widget( scroller )
		{
			Layout = Layout.Column(),
			VerticalSizeMode = SizeMode.CanGrow | SizeMode.Expand,
			MaximumWidth = menuWidth
		};

		// Actual registered modes
		foreach ( var mode in _modes )
		{
			var b = scroller.Canvas.Layout.Add( new ShrimpleRagdollModeMenuOption( mode, SerializedProperty ) );
			b.MouseLeftPress = () =>
			{
				SetValue( mode.Name );
				_menu.Close();
			};
		}

		_menu.Position = ScreenRect.BottomLeft;
		_menu.Visible = true;
		_menu.AdjustSize();
		_menu.ConstrainToScreen();
		_menu.OnPaintOverride = PaintMenuBackground;

		if ( scroller.VerticalScrollbar.Minimum != scroller.VerticalScrollbar.Maximum )
		{
			scroller.Canvas.MaximumWidth -= 8; // space for scrollbar
		}
	}

	private void SetValue( string name )
	{
		var newValue = new ShrimpleRagdollModeProperty( name );
		SerializedProperty.SetValue( newValue );
	}

	private bool PaintMenuBackground()
	{
		Paint.SetBrushAndPen( Theme.ControlBackground );
		Paint.DrawRect( Paint.LocalRect, 0 );
		return true;
	}
}

file class ShrimpleRagdollModeMenuOption : Widget
{
	private ShrimpleRagdollModeInfo _info;
	private SerializedProperty _property;

	public ShrimpleRagdollModeMenuOption( ShrimpleRagdollModeInfo info, SerializedProperty property ) : base( null )
	{
		_info = info;
		_property = property;

		Layout = Layout.Row();
		Layout.Margin = 8;
		VerticalSizeMode = SizeMode.CanGrow;

		// (no icon for now – you could add a per-mode icon later if you want)

		var c = Layout.AddColumn();

		var titleText = string.IsNullOrEmpty( info.Name ) ? "Unset" : info.Name;

		var title = c.Add( new Label( titleText ) );
		title.SetStyles( $"font-size: 12px; font-weight: bold; font-family: {Theme.DefaultFont}; color: white;" );

		if ( !string.IsNullOrWhiteSpace( info.Description ) )
		{
			var desc = c.Add( new Label( info.Description.Trim( '\n', '\r', '\t', ' ' ) ) );
			desc.WordWrap = true;
			desc.MinimumHeight = 1;
			desc.VerticalSizeMode = SizeMode.CanGrow;
		}
	}

	private bool HasValue()
	{
		var value = _property.GetValue<ShrimpleRagdollModeProperty>( "Disabled" );
		var currentName = value.Name ?? string.Empty;
		return string.Equals( currentName, _info.Name ?? string.Empty, StringComparison.Ordinal );
	}

	protected override void OnPaint()
	{
		if ( Paint.HasMouseOver || HasValue() )
		{
			Paint.SetBrushAndPen( Theme.Blue.WithAlpha( HasValue() ? 0.3f : 0.1f ) );
			Paint.DrawRect( LocalRect.Shrink( 2 ), 2 );
		}
	}
}
