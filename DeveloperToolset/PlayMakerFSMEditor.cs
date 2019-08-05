using UnityEngine;
using HutongGames.PlayMaker;
using System;
using System.Reflection;
using MSCLoader;
using System.Diagnostics;

namespace DeveloperToolset
{
	/// <summary>
	/// This class represents PlayMaker FSM editor GUI.
	/// </summary>
	public class PlayMakerFSMEditor
	{
		/// <summary>
		/// The instance of the component that is being currently edited. If null editor is disabled.
		/// </summary>
		private PlayMakerFSM m_editedComponent = null;

		/// <summary>
		/// The rect of the editor window.
		/// </summary>
		private Rect m_windowRect = new Rect(0, 0, 800, 600);

		/// <summary>
		/// Graph editor scroll view position.
		/// </summary>
		private Vector2 m_graphScrollView = new Vector2();

		/// <summary>
		/// State editor scroll view position.
		/// </summary>
		private Vector2 m_stateEditorScrollView = new Vector2();

		public bool IsPinned = false;

		private Vector2 m_previousEditedStateScrollView = new Vector2();
		private FsmState m_previousEditedState = null;
		private FsmState m_editedState = null;

		public void StartEdit(PlayMakerFSM fsmComponent) {
			m_editedComponent = fsmComponent;
			m_graphScrollView = new Vector2();
			m_stateEditorScrollView = new Vector2();
			m_previousEditedStateScrollView = new Vector2();
			m_previousEditedState = null;
			m_editedState = null;
		}

		public void OnGUI() {
			if (m_editedComponent == null) {
				return;
			}

			Fsm fsm = m_editedComponent.Fsm;
			GameObject editedGameObject = m_editedComponent.gameObject;
			m_windowRect = GUI.Window(1, m_windowRect, RenderWindow, "PlayMaker FSM Editor - " + fsm.Name + " (" + editedGameObject.name + ")");
		}

		private void RenderWindow(int windowID) {
			try {
				GUILayout.BeginVertical();

				Fsm fsm = m_editedComponent.Fsm;

				GUILayout.BeginHorizontal("sections");
				RenderGraph(fsm);
				RenderEditor(fsm);
				GUILayout.EndHorizontal();

				GUILayout.BeginHorizontal("buttons", GUILayout.Height(20.0f));
				if (GUILayout.Button("Close")) {
					StartEdit(null);
				}
				if (GUILayout.Button(IsPinned ? "Unpin" : "Pin")) {
					IsPinned = !IsPinned;
				}
				if (GUILayout.Button("Inspect owner game object")) {
					Inspector.SetInspect(m_editedComponent.gameObject.transform);
				}
				GUILayout.EndHorizontal();

				GUILayout.EndVertical();
				GUI.DragWindow();
			}
			catch (Exception e) {
				ModConsole.Error(e.Message);
				ModConsole.Error(e.StackTrace);
			}
		}

		static readonly Color SELECTED_ACTIVE_STATE_COLOR = new Color(0.7f, 0.92f, 0.20f);

		enum EditorState
		{
			State,
			Variables,
			Events,
		};

		private EditorState m_state = EditorState.State;

		private void RenderEditorTabButton(EditorState state, string text) {
			GUI.color = (state == m_state) ? Color.yellow : Color.white;
			if (GUILayout.Button(text)) {
				m_state = state;
			}
		}

		private void EditState(FsmState state) {
			m_previousEditedStateScrollView = m_stateEditorScrollView;
			m_previousEditedState = m_editedState;
			m_editedState = state;
		}

		private void RenderEditor(Fsm fsm) {
			GUILayout.BeginVertical("editor");

			GUILayout.BeginHorizontal("editorMenu");

			var previousColor = GUI.color;
			RenderEditorTabButton(EditorState.State, "State");
			RenderEditorTabButton(EditorState.Variables, "Variables");
			RenderEditorTabButton(EditorState.Events, "Events");

			GUI.color = previousColor;

			GUILayout.EndHorizontal();

			switch (m_state) {
				case EditorState.State:
					if (m_editedState != null) {
						RenderStateEditor(m_editedState, true);
					}
					else if (fsm.ActiveState != null) {
						// If no state is edited preview active state.
						RenderStateEditor(fsm.ActiveState, false);
					}
					break;
				case EditorState.Variables:
					RenderVariablesEditor(fsm);
					break;

				case EditorState.Events:
					RenderEventsEditor(fsm);
					break;
			}

			GUILayout.EndVertical();
		}

		private void RenderGraph(Fsm fsm) {
			m_graphScrollView = GUILayout.BeginScrollView(m_graphScrollView, GUILayout.Width(m_windowRect.width * 0.4f));

			foreach (var state in fsm.States) {
				GUILayout.BeginVertical();

				var previousColor = GUI.color;

				GUI.color = Color.red;

				bool isActiveState = fsm.ActiveState == state;
				if (isActiveState) {
					GUI.color = Color.green;
				}

				string stateName = state.Name;
				if (m_editedState == state) {
					stateName += " (EDITED)";

					if (isActiveState) {
						GUI.color = SELECTED_ACTIVE_STATE_COLOR;
					}
					else {
						GUI.color = Color.yellow;
					}
				}

				if (GUILayout.Button(stateName)) {
					if (m_editedState == state) {
						EditState(null);
					}
					else {
						EditState(state);
					}
				}

				GUI.color = previousColor;

				foreach (var transition in state.Transitions) {
					if (GUILayout.Button(transition.EventName + " -> " + transition.ToState)) {
						EditState(fsm.GetState(transition.ToState));
					}
				}

				GUILayout.EndVertical();
			}

			GUILayout.EndScrollView();
		}

		private void RenderStateEditor(FsmState editedState, bool edit) {
			GUILayout.BeginVertical("stateEditor");
			GUILayout.BeginHorizontal();

			if (m_previousEditedState != null) {
				if (GUILayout.Button("<", GUILayout.Width(20.0f))) {
					m_stateEditorScrollView = m_previousEditedStateScrollView;
					EditState(m_previousEditedState);
				}
			}

			if (edit) {
				GUILayout.Label(editedState.Name + " state editor");
			}
			else {
				GUILayout.Label(editedState.Name + " current state preview");
			}
			GUILayout.EndHorizontal();

			m_stateEditorScrollView = GUILayout.BeginScrollView(m_stateEditorScrollView);
			foreach (var action in editedState.Actions) {
				GUILayout.BeginVertical("action");
				GUILayout.Button(action.ToString());

				var fields = action.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance);
				foreach (var fieldInfo in fields) {
					GUILayout.BeginHorizontal();
					GUILayout.Box(fieldInfo.Name, GUILayout.Width(100));
					try {
						var fieldValue = fieldInfo.GetValue(action);
						var fieldValueStr = fieldValue.ToString();
						if (!(fieldValue is FsmFloat)) {
							fieldValueStr = fieldValueStr.Substring(fieldValueStr.LastIndexOf(".", System.StringComparison.Ordinal) + 1);
						}
						if (fieldValue is FsmOwnerDefault) {
							var property = fieldValue as FsmOwnerDefault;

							switch (property.OwnerOption) {
								case OwnerDefaultOption.SpecifyGameObject:
									if (property.GameObject.Value != null) {
										if (GUILayout.Button(property.GameObject.Value.name)) {
											Inspector.SetInspect(property.GameObject.Value.transform);
										}
									}
									else {
										GUILayout.Box("None");
									}
									break;
								case OwnerDefaultOption.UseOwner:
									GUILayout.Box("Use owner");
									break;
							}
						}
						else if (fieldValue is FsmProperty) {
							var property = fieldValue as FsmProperty;
							GUILayout.Box("(" + property.PropertyName + ")");
							GUILayout.Box("target: " + property.TargetObject + "");
						}
						else if (fieldValue is NamedVariable) {
							var named = fieldValue as NamedVariable;
							GUILayout.Box(fieldValueStr + "(" + named.Name + ")");
						}
						else if (fieldValue is FsmEvent) {
							var evnt = fieldValue as FsmEvent;

							FsmState targetState = null;
							string targetStateName = "(NO TRANSITION)";
							foreach (var transition in editedState.Transitions) {
								if (transition.FsmEvent == evnt) {
									targetState = editedState.Fsm.GetState(transition.ToState);
									targetStateName = transition.ToState;
									break;
								}
							}

							if (GUILayout.Button("Event " + evnt.Name + " -> " + targetStateName)) {
								EditState(targetState);
							}
						}
						else {
							GUILayout.Label(fieldValueStr);
						}
					}
					catch (Exception) {
					}
					GUILayout.EndHorizontal();
				}
				GUILayout.EndVertical();
			}
			GUILayout.EndScrollView();
			GUILayout.EndVertical();
		}

		private string GetNamedVariableTypeName(NamedVariable variable) {
			if (variable is FsmTexture) {
				return "TEX";
			}
			else if (variable is FsmMaterial) {
				return "MAT";
			}
			else if (variable is FsmFloat) {
				return "FLOAT";
			}
			else if (variable is FsmBool) {
				return "BOOL";
			}
			else if (variable is FsmString) {
				return "STR";
			}
			else if (variable is FsmVector2) {
				return "VEC2";
			}
			else if (variable is FsmInt) {
				return "INT";
			}
			else if (variable is FsmRect) {
				return "RECT";
			}
			else if (variable is FsmQuaternion) {
				return "QUAT";
			}
			else if (variable is FsmColor) {
				return "COLOR";
			}
			else if (variable is FsmGameObject) {
				return "GAMEOBJ";
			}
			else if (variable is FsmObject) {
				return "OBJ";
			}
			else if (variable is FsmVector3) {
				return "VEC3";
			}
			return "UNSUPPORTED";
		}
		private void RenderVariableEditor(NamedVariable variable) {
			GUILayout.BeginHorizontal();
			GUILayout.Box(GetNamedVariableTypeName(variable), GUILayout.Width(60.0f));
			GUILayout.Box(variable.Name, GUILayout.Width(120.0f));

			try {
				if (variable is FsmTexture) {
					var typedVariable = variable as FsmTexture;
					GUILayout.Box(typedVariable.Value);
				}
				else if (variable is FsmMaterial) {
					var typedVariable = variable as FsmMaterial;
					GUILayout.Box(typedVariable.Value.name);
				}
				else if (variable is FsmFloat) {
					var typedVariable = variable as FsmFloat;
					typedVariable.Value = (float)Convert.ToDouble(GUILayout.TextField(typedVariable.Value.ToString()));
				}
				else if (variable is FsmBool) {
					var typedVariable = variable as FsmBool;
					typedVariable.Value = GUILayout.Toggle(typedVariable.Value, "Value");
				}
				else if (variable is FsmString) {
					var typedVariable = variable as FsmString;
					typedVariable.Value = GUILayout.TextArea(typedVariable.Value);
				}
				else if (variable is FsmVector2) {
					var typedVariable = variable as FsmVector2;
					GUILayout.Box(typedVariable.Value.ToString());
				}
				else if (variable is FsmInt) {
					var typedVariable = variable as FsmInt;
					typedVariable.Value = Convert.ToInt32(GUILayout.TextField(typedVariable.Value.ToString()));
				}
				else if (variable is FsmRect) {
					var typedVariable = variable as FsmRect;
					GUILayout.Box(typedVariable.Value.ToString());
				}
				else if (variable is FsmQuaternion) {
					var typedVariable = variable as FsmQuaternion;
					GUILayout.Box(typedVariable.Value.ToString());
				}
				else if (variable is FsmColor) {
					var typedVariable = variable as FsmColor;
					GUILayout.Box(typedVariable.Value.ToString());
				}
				else if (variable is FsmGameObject) {
					var typedVariable = variable as FsmGameObject;
					if (typedVariable.Value == null) {
						GUILayout.Box("(NULL)");
					}
					else {
						if (GUILayout.Button(typedVariable.Value.name)) {
							Inspector.SetInspect(typedVariable.Value.transform);
						}
					}
				}
				else if (variable is FsmObject) {
					var typedVariable = variable as FsmObject;
					if (typedVariable.Value == null) {
						GUILayout.Box("(NULL)");
					}
					else {
						GUILayout.Box(typedVariable.Value.ToString());
					}
				}
				else if (variable is FsmVector3) {
					var typedVariable = variable as FsmVector3;
					GUILayout.Box(typedVariable.Value.ToString());
				}
			}
			catch (Exception e) {
				GUILayout.Box("ERROR " + e.Message);
			}
			GUILayout.EndVertical();
		}

		private void RenderVariablesEditor(Fsm fsm) {
			GUILayout.BeginVertical("variablesEditor", GUILayout.ExpandHeight(true));
			GUILayout.BeginHorizontal();
			GUILayout.Box("Type", GUILayout.Width(60.0f));
			GUILayout.Box("Name", GUILayout.Width(120.0f));
			GUILayout.Box("Value");
			GUILayout.EndHorizontal();

			foreach (var variable in fsm.Variables.TextureVariables) {
				RenderVariableEditor(variable);
			}
			foreach (var variable in fsm.Variables.MaterialVariables) {
				RenderVariableEditor(variable);
			}
			foreach (var variable in fsm.Variables.FloatVariables) {
				RenderVariableEditor(variable);
			}
			foreach (var variable in fsm.Variables.BoolVariables) {
				RenderVariableEditor(variable);
			}
			foreach (var variable in fsm.Variables.StringVariables) {
				RenderVariableEditor(variable);
			}
			foreach (var variable in fsm.Variables.Vector2Variables) {
				RenderVariableEditor(variable);
			}
			foreach (var variable in fsm.Variables.IntVariables) {
				RenderVariableEditor(variable);
			}
			foreach (var variable in fsm.Variables.RectVariables) {
				RenderVariableEditor(variable);
			}
			foreach (var variable in fsm.Variables.QuaternionVariables) {
				RenderVariableEditor(variable);
			}
			foreach (var variable in fsm.Variables.ColorVariables) {
				RenderVariableEditor(variable);
			}
			foreach (var variable in fsm.Variables.GameObjectVariables) {
				RenderVariableEditor(variable);
			}
			foreach (var variable in fsm.Variables.ObjectVariables) {
				RenderVariableEditor(variable);
			}
			foreach (var variable in fsm.Variables.Vector3Variables) {
				RenderVariableEditor(variable);
			}
			GUILayout.EndVertical();
		}

		private void RenderEventsEditor(Fsm fsm) {

			foreach (var e in fsm.Events) {

				GUILayout.BeginHorizontal("event");
				GUILayout.Box(e.Name);

				if (e.IsGlobal) {
					GUILayout.Box("GLOBAL");
				}

				GUILayout.EndHorizontal();
			}
		}
	}
}
