using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AlternativePlay
{

internal sealed class TwinNalulunaVisual : IDisposable
{
	internal sealed class NativeVisibility : IDisposable
	{
		private readonly Saber saber;

		private readonly GameObject originalStock;

		private readonly Transform replacementRoot;

		private readonly Dictionary<Renderer, bool> hidden = new Dictionary<Renderer, bool>();

		private Api api;

		private float nextScan;

		internal NativeVisibility(Saber saber, GameObject originalStock, Transform replacementRoot)
		{
			this.saber = saber;
			this.originalStock = originalStock;
			this.replacementRoot = replacementRoot;
		}

		private bool OwnsReplacement(Transform transform)
		{
			Transform val = transform;
			while (val)
			{
				if ((Object)(object)val == (Object)(object)replacementRoot)
				{
					return true;
				}
				val = val.parent;
			}
			return false;
		}

		internal void Synchronize()
		{
			if (Time.unscaledTime >= nextScan)
			{
				nextScan = Time.unscaledTime + 0.25f;
				HashSet<Renderer> hashSet = new HashSet<Renderer>();
				Renderer[] componentsInChildren;
				if (originalStock)
				{
					componentsInChildren = originalStock.GetComponentsInChildren<Renderer>(true);
					foreach (Renderer val in componentsInChildren)
					{
						if (!OwnsReplacement(((Component)val).transform))
						{
							hashSet.Add(val);
						}
					}
				}
				if (originalStock)
				{
					Behaviour[] componentsInChildren2 = originalStock.GetComponentsInChildren<Behaviour>(true);
					foreach (Behaviour val2 in componentsInChildren2)
					{
						if (!(((object)val2).GetType().FullName == "SaberTrail") || OwnsReplacement(((Component)val2).transform))
						{
							continue;
						}
						object obj = ((object)val2).GetType().GetField("_trailRenderer", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(val2);
						Component val3 = (Component)((obj is Component) ? obj : null);
						if (val3)
						{
							componentsInChildren = val3.GetComponentsInChildren<Renderer>(true);
							foreach (Renderer item in componentsInChildren)
							{
								hashSet.Add(item);
							}
						}
					}
				}
				try
				{
					if (api == null)
					{
						api = Api.Find();
					}
					Component val4 = ((api != null && saber) ? ((Component)saber).GetComponent(api.VisualType) : null);
					if (val4)
					{
						PropertyInfo[] roots = api.Roots;
						for (int i = 0; i < roots.Length; i++)
						{
							GameObject val5 = (GameObject)roots[i].GetValue(val4);
							if (!val5)
							{
								continue;
							}
							componentsInChildren = val5.GetComponentsInChildren<Renderer>(true);
							foreach (Renderer val6 in componentsInChildren)
							{
								if (!OwnsReplacement(((Component)val6).transform))
								{
									hashSet.Add(val6);
								}
							}
							Behaviour[] componentsInChildren2 = val5.GetComponentsInChildren<Behaviour>(true);
							foreach (Behaviour val7 in componentsInChildren2)
							{
								if (((object)val7).GetType() == api.TrailType && !OwnsReplacement(((Component)val7).transform))
								{
									Renderer val8 = (Renderer)api.TrailRenderer.GetValue(val7);
									if (val8)
									{
										hashSet.Add(val8);
									}
								}
							}
						}
					}
				}
				catch
				{
				}
				componentsInChildren = hidden.Keys.ToArray();
				foreach (Renderer val9 in componentsInChildren)
				{
					if (!hashSet.Contains(val9))
					{
						if (val9)
						{
							val9.forceRenderingOff = hidden[val9];
						}
						hidden.Remove(val9);
					}
				}
				foreach (Renderer item2 in hashSet)
				{
					if (item2 && !hidden.ContainsKey(item2))
					{
						hidden.Add(item2, item2.forceRenderingOff);
					}
				}
			}
			foreach (KeyValuePair<Renderer, bool> item3 in hidden)
			{
				if (item3.Key)
				{
					item3.Key.forceRenderingOff = true;
				}
			}
		}

		public void Dispose()
		{
			foreach (KeyValuePair<Renderer, bool> item in hidden)
			{
				if (item.Key)
				{
					item.Key.forceRenderingOff = item.Value;
				}
			}
			hidden.Clear();
		}
	}

	private sealed class Api
	{
		internal static readonly FieldInfo Handle = typeof(Saber).GetField("_handleTransform", BindingFlags.Instance | BindingFlags.NonPublic);

		internal Type VisualType;

		internal Type TrailType;

		internal Type EndpointType;

		internal FieldInfo EndpointOrigin;

		internal FieldInfo EndpointPoint;

		internal PropertyInfo[] Roots;

		internal FieldInfo[] Fields;

		internal PropertyInfo Layer;

		internal PropertyInfo TrailRenderer;

		internal MethodInfo Initialize;

		internal static Api Find()
		{
			Assembly assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault((Assembly a) => a.GetName().Name == "NalulunaSaber");
			if (assembly == null)
			{
				return null;
			}
			if (assembly.GetName().Version != new Version(2, 1, 4, 0))
			{
				throw new NotSupportedException("NalulunaSaber adapter requires the inspected version 2.1.4.");
			}
			Api api = new Api();
			api.VisualType = assembly.GetType("NalulunaSaber.NalulunaSaber", throwOnError: true);
			api.Roots = new string[4] { "saberF", "saberT", "trailF", "trailT" }.Select((string n) => api.VisualType.GetProperty(n)).ToArray();
			api.TrailType = assembly.GetType("DfOjxzQkk5oBlY2flGbCzIYcAXsYQBtj1vK48LU$g87Y", throwOnError: true);
			api.Fields = api.TrailType.GetFields(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public);
			api.Layer = api.TrailType.GetProperty("sMOEa2j1PXZHs8nKQXE8oZU");
			api.TrailRenderer = api.TrailType.GetProperty("TM9EteOlOkOWy4ATodRGlZ4pxjPVd_FL0UKJLaAEtqq7", BindingFlags.Instance | BindingFlags.NonPublic);
			api.Initialize = api.TrailType.GetMethod("MkKH$erg0y6IKsKaVOWmXH4YCupmvygTaLmbkK11bNqn", Type.EmptyTypes);
			api.EndpointType = assembly.GetType("Sgz2Zs6_pod_Xfdb5nG6wDrkjbA4EoFN2tm0VcrC7JlY6SzVLZCnRIldRZo4fy3CsA", throwOnError: true);
			api.EndpointOrigin = api.EndpointType.GetField("AKbhCDBydDqiFnz7pSz_itM");
			api.EndpointPoint = api.EndpointType.GetField("w$Uo0UbXK831LjhodCAuTI4");
			if (Handle == null || api.Roots.Any((PropertyInfo p) => p == null || p.PropertyType != typeof(GameObject)) || api.EndpointOrigin?.FieldType != typeof(Transform) || api.EndpointPoint?.FieldType != typeof(Transform) || api.Layer == null || api.TrailRenderer == null || api.Initialize == null || api.Fields.Length != 10 || api.Fields.Count((FieldInfo f) => f.FieldType == typeof(Transform)) != 2 || api.Fields.Any((FieldInfo f) => f.FieldType != typeof(float) && f.FieldType != typeof(int) && f.FieldType != typeof(bool) && f.FieldType != typeof(Color) && f.FieldType != typeof(Material) && f.FieldType != typeof(Transform)))
			{
				throw new NotSupportedException("Unexpected NalulunaSaber visual/trail API layout.");
			}
			return api;
		}
	}

	private sealed class VisualTree : IDisposable
	{
		private readonly GameObject source;

		private readonly Api api;

		private readonly GameObject root;

		private readonly Dictionary<Transform, Transform> nodes = new Dictionary<Transform, Transform>();

		private readonly List<RenderPair> renderers = new List<RenderPair>();

		private readonly List<TrailPair> trails = new List<TrailPair>();

		private readonly List<Component> endpointHelpers = new List<Component>();

		private Component[] sourceComponents;

		private bool initialized;

		internal GameObject Source => source;

		internal bool HasModel => renderers.Count > 0;

		internal bool HasTrail => trails.Count > 0;

		internal VisualTree(GameObject source, Transform parent, Api api)
		{
			this.source = source;
			this.api = api;
			root = new GameObject("Twin " + ((Object)source).name);
			root.SetActive(false);
			root.transform.SetParent(parent, false);
		}

		internal void Build()
		{
			CopyNodes(source.transform, root.transform);
			sourceComponents = source.GetComponentsInChildren<Component>(true);
			Component[] array = sourceComponents;
			foreach (Component val in array)
			{
				Renderer val2 = (Renderer)(object)((val is Renderer) ? val : null);
				if (val2 != null)
				{
					if (!(val2 is MeshRenderer) && !(val2 is SkinnedMeshRenderer))
					{
						AlternativePlay.Logger.Warn("Naluluna visual decoration omitted: " + ((object)val2).GetType().FullName + " in " + ((Object)source).name);
					}
					else
					{
						renderers.Add(new RenderPair(val2, ((Component)nodes[((Component)val2).transform]).gameObject));
					}
				}
				else if (val && ((object)val).GetType() == api.TrailType)
				{
					trails.Add(new TrailPair((Behaviour)val, ((Component)nodes[val.transform]).gameObject, nodes, api));
				}
				else if (val && ((object)val).GetType() == api.EndpointType)
				{
					endpointHelpers.Add(val);
				}
			}
			AlternativePlay.Logger.Info("Twin Naluluna visual tree: " + ((Object)source).name + ", transforms=" + nodes.Count + ", mesh renderers=" + renderers.Count + ", independent Naluluna trails=" + trails.Count + ", endpoint helpers=" + endpointHelpers.Count + ".");
		}

		private void CopyNodes(Transform from, Transform to)
		{
			nodes.Add(from, to);
			for (int i = 0; i < from.childCount; i++)
			{
				Transform child = from.GetChild(i);
				Transform transform = new GameObject(((Object)child).name).transform;
				transform.SetParent(to, false);
				CopyNodes(child, transform);
			}
		}

		internal bool Matches()
		{
			if (!source || nodes.Any((KeyValuePair<Transform, Transform> p) => !p.Key || p.Key.childCount != p.Value.childCount))
			{
				return false;
			}
			if (source.GetComponentsInChildren<Component>(true).SequenceEqual(sourceComponents))
			{
				return trails.All((TrailPair t) => t.Matches());
			}
			return false;
		}

		internal void Synchronize(Transform saber, Transform handle)
		{
			foreach (KeyValuePair<Transform, Transform> node in nodes)
			{
				if (node.Key)
				{
					if ((Object)(object)node.Key == (Object)(object)source.transform)
					{
						Quaternion val = Quaternion.Inverse(saber.rotation);
						node.Value.localPosition = val * (node.Key.position - handle.position);
						node.Value.localRotation = val * node.Key.rotation;
						node.Value.localScale = node.Key.lossyScale;
					}
					else
					{
						node.Value.localPosition = node.Key.localPosition;
						node.Value.localRotation = node.Key.localRotation;
						node.Value.localScale = node.Key.localScale;
						((Component)node.Value).gameObject.SetActive(((Component)node.Key).gameObject.activeSelf);
					}
					((Component)node.Value).gameObject.layer = ((Component)node.Key).gameObject.layer;
				}
			}
			foreach (RenderPair renderer in renderers)
			{
				renderer.Synchronize();
			}
			foreach (Component endpointHelper in endpointHelpers)
			{
				if (endpointHelper)
				{
					Transform val2 = (Transform)api.EndpointOrigin.GetValue(endpointHelper);
					Transform val3 = (Transform)api.EndpointPoint.GetValue(endpointHelper);
					if (val2 && val3)
					{
						nodes[endpointHelper.transform].localPosition = val2.InverseTransformPoint(val3.position);
					}
				}
			}
			foreach (TrailPair trail in trails)
			{
				trail.Synchronize();
			}
			if (!initialized)
			{
				root.SetActive(true);
				foreach (TrailPair trail2 in trails)
				{
					trail2.Initialize();
				}
				initialized = true;
			}
			root.SetActive(source && source.activeSelf);
			foreach (TrailPair trail3 in trails)
			{
				trail3.RefreshVisibility();
			}
		}

		public void Dispose()
		{
			if (root)
			{
				root.SetActive(false);
			}
			foreach (TrailPair trail in trails)
			{
				trail.Dispose();
			}
			if (root)
			{
				Object.Destroy((Object)(object)root);
			}
			foreach (RenderPair renderer in renderers)
			{
				renderer.Dispose();
			}
		}
	}

	private sealed class RenderPair : IDisposable
	{
		private readonly Renderer source;

		private readonly MeshRenderer target;

		private readonly MeshFilter filter;

		private readonly Mesh baked;

		private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();

		internal RenderPair(Renderer source, GameObject target)
		{
			this.source = source;
			filter = target.AddComponent<MeshFilter>();
			this.target = target.AddComponent<MeshRenderer>();
			if (source is SkinnedMeshRenderer)
			{
				baked = new Mesh();
			}
		}

		internal void Synchronize()
		{
			if (!source)
			{
				((Renderer)target).enabled = false;
				return;
			}
			Renderer obj = source;
			SkinnedMeshRenderer val = (SkinnedMeshRenderer)(object)((obj is SkinnedMeshRenderer) ? obj : null);
			if (val != null)
			{
				val.BakeMesh(baked);
				filter.sharedMesh = baked;
			}
			else
			{
				MeshFilter obj2 = filter;
				MeshFilter component = ((Component)source).GetComponent<MeshFilter>();
				obj2.sharedMesh = ((component != null) ? component.sharedMesh : null);
			}
			Material[] sharedMaterials = source.sharedMaterials;
			((Renderer)target).sharedMaterials = sharedMaterials;
			source.GetPropertyBlock(properties);
			((Renderer)target).SetPropertyBlock(properties);
			for (int i = 0; i < sharedMaterials.Length; i++)
			{
				source.GetPropertyBlock(properties, i);
				((Renderer)target).SetPropertyBlock(properties, i);
			}
			((Renderer)target).enabled = source.enabled;
			((Renderer)target).shadowCastingMode = source.shadowCastingMode;
			((Renderer)target).receiveShadows = source.receiveShadows;
			((Renderer)target).sortingLayerID = source.sortingLayerID;
			((Renderer)target).sortingOrder = source.sortingOrder;
			((Renderer)target).lightProbeUsage = source.lightProbeUsage;
			((Renderer)target).reflectionProbeUsage = source.reflectionProbeUsage;
		}

		public void Dispose()
		{
			if (baked)
			{
				Object.Destroy((Object)(object)baked);
			}
		}
	}

	private sealed class TrailPair
	{
		private readonly Behaviour source;

		private readonly Behaviour target;

		private readonly Api api;

		private readonly Dictionary<Transform, Transform> nodes;

		private readonly object[] settings;

		internal TrailPair(Behaviour source, GameObject target, Dictionary<Transform, Transform> nodes, Api api)
		{
			this.source = source;
			this.nodes = nodes;
			this.api = api;
			this.target = (Behaviour)target.AddComponent(api.TrailType);
			settings = api.Fields.Select((FieldInfo f) => f.GetValue(source)).ToArray();
			Synchronize();
		}

		internal bool Matches()
		{
			if (!source)
			{
				return false;
			}
			for (int i = 0; i < api.Fields.Length; i++)
			{
				if (api.Fields[i].FieldType != typeof(Color) && api.Fields[i].Name != "XZ3AYM149fHTingjRhNAgVfqKcIR$NE8vn$Va4_JjHV6" && !object.Equals(settings[i], api.Fields[i].GetValue(source)))
				{
					return false;
				}
			}
			return true;
		}

		internal void Synchronize()
		{
			if (!source)
			{
				target.enabled = false;
				return;
			}
			FieldInfo[] fields = api.Fields;
			foreach (FieldInfo obj in fields)
			{
				object obj2 = obj.GetValue(source);
				if (obj.FieldType == typeof(Transform))
				{
					Transform val = (Transform)((obj2 is Transform) ? obj2 : null);
					if (val == null || !nodes.TryGetValue(val, out var value))
					{
						throw new NotSupportedException("Custom trail endpoint is outside its visual tree.");
					}
					obj2 = value;
				}
				obj.SetValue(target, obj2);
			}
			api.Layer.SetValue(target, api.Layer.GetValue(source));
			target.enabled = source.enabled;
		}

		internal void Initialize()
		{
			api.Initialize.Invoke(target, null);
		}

		internal void RefreshVisibility()
		{
			MeshRenderer val = (MeshRenderer)api.TrailRenderer.GetValue(target);
			if (val)
			{
				((Renderer)val).enabled = target.isActiveAndEnabled;
			}
		}

		internal void Dispose()
		{
			if (!target)
			{
				return;
			}
			MeshRenderer val = (MeshRenderer)api.TrailRenderer.GetValue(target);
			if (val)
			{
				((Renderer)val).enabled = false;
				MeshFilter component = ((Component)val).GetComponent<MeshFilter>();
				Mesh val2 = ((component != null) ? component.sharedMesh : null);
				if (val2)
				{
					Object.Destroy((Object)(object)val2);
				}
			}
		}
	}

	private readonly Saber twin;

	private readonly SaberManager manager;

	private readonly GameObject stock;

	private readonly Transform visualParent;

	private readonly Renderer[] stockRenderers;

	private readonly Behaviour[] stockTrails;

	private readonly List<VisualTree> trees = new List<VisualTree>();

	private readonly GameObject[] observed = (GameObject[])(object)new GameObject[4];

	private Api api;

	private bool probed;

	private bool retryPending;

	private Component provider;

	private Saber reference;

	private Transform handle;

	private float nextCheck;

	private float nextRetry;

	private float nextProbe;

	private float nextWarning;

	internal TwinNalulunaVisual(Saber twin, SaberManager manager, GameObject stock, Transform visualParent = null)
	{
		this.twin = twin;
		this.manager = manager;
		this.stock = stock;
		this.visualParent = (visualParent ? visualParent : ((Component)twin).transform);
		stockRenderers = stock.GetComponentsInChildren<Renderer>(true);
		stockTrails = (from b in stock.GetComponentsInChildren<Behaviour>(true)
			where ((object)b).GetType().FullName == "SaberTrail"
			select b).ToArray();
	}

	internal void Synchronize()
	{
		if (!manager || (retryPending && Time.unscaledTime < nextRetry))
		{
			return;
		}
		try
		{
			if (!probed || (api == null && Time.unscaledTime >= nextProbe))
			{
				probed = true;
				nextProbe = Time.unscaledTime + 2f;
				api = Api.Find();
			}
			if (api == null)
			{
				return;
			}
			Saber val = (((int)twin.saberType == 0) ? manager.leftSaber : manager.rightSaber);
			Component val2 = (val ? ((Component)val).GetComponent(api.VisualType) : null);
			if ((Object)(object)val != (Object)(object)reference || (Object)(object)val2 != (Object)(object)provider)
			{
				Clear();
				reference = val;
				provider = val2;
				handle = ((!val) ? ((Transform)null) : ((Transform)Api.Handle.GetValue(val)));
			}
			if (!provider || !handle)
			{
				Clear();
				return;
			}
			bool flag = retryPending;
			for (int i = 0; i < observed.Length; i++)
			{
				GameObject val3 = (GameObject)api.Roots[i].GetValue(provider);
				if (!val3)
				{
					val3 = null;
				}
				if (observed[i] != val3)
				{
					flag = true;
				}
				observed[i] = val3;
			}
			if (Time.unscaledTime >= nextCheck)
			{
				nextCheck = Time.unscaledTime + 0.25f;
				foreach (VisualTree tree in trees)
				{
					if (!tree.Matches())
					{
						flag = true;
					}
				}
			}
			if (flag)
			{
				ClearTrees();
				HashSet<GameObject> hashSet = new HashSet<GameObject>();
				GameObject[] array = observed;
				foreach (GameObject val4 in array)
				{
					if (val4 && hashSet.Add(val4))
					{
						VisualTree visualTree = new VisualTree(val4, visualParent, api);
						trees.Add(visualTree);
						visualTree.Build();
					}
				}
				AlternativePlay.Logger.Info("Twin NalulunaSaber 2.1.4: " + twin.saberType.ToString() + " uses " + string.Join(", ", hashSet.Select((GameObject x) => ((Object)x).name)) + "; independent visual roots=" + trees.Count + ".");
			}
			foreach (VisualTree tree2 in trees)
			{
				tree2.Synchronize(((Component)reference).transform, handle);
			}
			retryPending = false;
			bool flag2 = trees.Any((VisualTree t) => t.HasModel && ((Object)(object)t.Source == (Object)(object)observed[0] || (Object)(object)t.Source == (Object)(object)observed[1]));
			SetStock(!flag2, !trees.Any((VisualTree t) => t.HasTrail));
		}
		catch (Exception ex)
		{
			retryPending = true;
			nextRetry = Time.unscaledTime + 1f;
			ClearTrees();
			if (Time.unscaledTime >= nextWarning)
			{
				nextWarning = Time.unscaledTime + 10f;
				AlternativePlay.Logger.Warn("Twin NalulunaSaber visual fallback (will retry): " + ex);
			}
		}
	}

	private void ClearTrees()
	{
		foreach (VisualTree tree in trees)
		{
			tree.Dispose();
		}
		trees.Clear();
		SetStock(modelVisible: true, trailVisible: true);
	}

	private void Clear()
	{
		ClearTrees();
		Array.Clear(observed, 0, observed.Length);
	}

	public void Dispose()
	{
		Clear();
	}

	private void SetStock(bool modelVisible, bool trailVisible)
	{
		if (stock)
		{
			stock.SetActive(modelVisible || trailVisible);
		}
		Renderer[] array = stockRenderers;
		foreach (Renderer val in array)
		{
			if (val)
			{
				val.forceRenderingOff = !modelVisible;
			}
		}
		Behaviour[] array2 = stockTrails;
		foreach (Behaviour val2 in array2)
		{
			if (val2)
			{
				val2.enabled = trailVisible;
			}
		}
	}
}
}
