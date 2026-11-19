using ImGuiNET;

namespace OpenTPW.ModKit;

[HandlesExtension( ".sam" )]
internal class SettingsViewer : IFileViewer
{
	private SettingsFile settingsFile;

	public SettingsViewer( string fileName )
	{
		settingsFile = new SettingsFile( fileName );
	}

	public void DrawPreview()
	{
		if ( ImGui.BeginTable( "##SettingsTable",
						2,
						ImGuiTableFlags.PadOuterX | ImGuiTableFlags.Borders | ImGuiTableFlags.ScrollY,
						new Vector2( 0, 325 ) ) )
		{
			ImGui.TableSetupColumn( $"Name", ImGuiTableColumnFlags.DefaultSort, 0.75f );
			ImGui.TableSetupColumn( $"Value", ImGuiTableColumnFlags.DefaultSort, 0.25f );
			ImGui.TableHeadersRow();

			foreach ( var setting in settingsFile.Entries )
			{
				ImGui.TableNextColumn();
				ImGui.Text( setting.Key );

				if ( ImGui.IsItemHovered() )
					ImGui.SetTooltip( setting.Key );

				ImGui.TableNextColumn();
				ImGui.Text( setting.Value );

				if ( ImGui.IsItemHovered() )
					ImGui.SetTooltip( setting.Value );
			}
		}

		ImGui.EndTable();
	}

}
