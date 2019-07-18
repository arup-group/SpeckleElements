﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using SpeckleCore;
using SpeckleCoreGeometryClasses;
using SpeckleElements;

namespace SpeckleElementsGSA
{
  [GSAObject("EL.4", new string[] { "NODE.2", "PROP_2D" }, "elements", true, false, new Type[] { typeof(GSANode), typeof(GSA2DProperty) }, new Type[] { typeof(GSA2DProperty) })]
  public class GSA2DElement : IGSASpeckleContainer
  {
    public string Member;

    public int GSAId { get; set; }
    public string GWACommand { get; set; }
    public List<string> SubGWACommand { get; set; } = new List<string>();
    public dynamic Value { get; set; } = new Structural2DElement();

    public void ParseGWACommand(GSAInterfacer GSA, List<GSANode> nodes, List<GSA2DProperty> props)
    {
      if (this.GWACommand == null)
        return;

      Structural2DElement obj = new Structural2DElement();

      string[] pieces = this.GWACommand.ListSplit("\t");

      int counter = 1; // Skip identifier
      this.GSAId = Convert.ToInt32(pieces[counter++]);
      obj.ApplicationId = GSA.GetSID(this.GetGSAKeyword(), this.GSAId);
      obj.Name = pieces[counter++].Trim(new char[] { '"' });
      var color = pieces[counter++].ParseGSAColor();

      string type = pieces[counter++];
      if (color != null)
        obj.Colors = Enumerable.Repeat(color.HexToArgbColor().Value, type.ParseElementNumNodes()).ToList();
      else
        obj.Colors = new List<int>();

      obj.ElementType = Structural2DElementType.Generic;
      var propertyGSAId = Convert.ToInt32(pieces[counter++]);
      obj.PropertyRef = GSA.GetSID(typeof(GSA2DProperty).GetGSAKeyword(), propertyGSAId);
      counter++; // Group

      obj.Vertices = new List<double>();
      obj.Faces = new List<int>() { type.ParseElementNumNodes() - 3 };

      for (int i = 0; i < type.ParseElementNumNodes(); i++)
      {
        string key = pieces[counter++];
        GSANode node = nodes.Where(n => n.GSAId.ToString() == key).FirstOrDefault();
        obj.Vertices.AddRange(node.Value.Value);
        obj.Faces.Add(i);
        this.SubGWACommand.Add(node.GWACommand);
      }

      counter++; // Orientation node

      GSA2DProperty prop = props.Where(p => p.Value.ApplicationId == obj.PropertyRef).FirstOrDefault();
      obj.Axis = GSA.Parse2DAxis(obj.Vertices.ToArray(),
          Convert.ToDouble(pieces[counter++]),
          prop == null ? false : (prop as GSA2DProperty).IsAxisLocal);
      if (prop != null)
        this.SubGWACommand.Add(prop.GWACommand);

      if (pieces[counter++] != "NO_RLS")
      {
        string start = pieces[counter++];
        string end = pieces[counter++];

        counter += start.Split('K').Length - 1 + end.Split('K').Length - 1;
      }

      counter++; //Ofsset x-start
      counter++; //Ofsset x-end
      counter++; //Ofsset y

      var (offset, offsetRec) = GSA.GetGSATotal2DElementOffset(propertyGSAId, Convert.ToDouble(pieces[counter++]));
      this.SubGWACommand.Add(offsetRec);

      obj.Offset = offset;

      counter++; // Dummy

      if (counter < pieces.Length)
        Member = pieces[counter++];

      this.Value = obj;
    }

    public void SetGWACommand(GSAInterfacer GSA, int group = 0)
    {
      if (this.Value == null)
        return;

      Structural2DElement mesh = this.Value as Structural2DElement;

      string keyword = typeof(GSA2DElement).GetGSAKeyword();

      int index = GSA.Indexer.ResolveIndex(typeof(GSA2DElement), mesh);
      int propRef = 0;
      try
      {
        propRef = GSA.Indexer.LookupIndex(typeof(GSA2DProperty), mesh.PropertyRef).Value;
      }
      catch { }

      List<string> ls = new List<string>();

      ls.Add("SET");
      ls.Add(keyword + ":" + GSA.GenerateSID(mesh));
      ls.Add(index.ToString());
      ls.Add(mesh.Name == null || mesh.Name == "" ? " " : mesh.Name);
      ls.Add(mesh.Colors == null || mesh.Colors.Count() < 1 ? "NO_RGB" : mesh.Colors[0].ArgbToHexColor().ToString());
      ls.Add(mesh.Vertices.Count() / 3 == 3 ? "TRI3" : "QUAD4");
      ls.Add(propRef.ToString());
      ls.Add(group.ToString()); // Group
      int numVertices = mesh.Faces[0] + 3;
      List<double> coor = new List<double>();
      for (int i = 1; i < mesh.Faces.Count(); i++)
      {
        coor.AddRange(mesh.Vertices.Skip(mesh.Faces[i] * 3).Take(3));
        ls.Add(GSA.NodeAt(mesh.Vertices[mesh.Faces[i] * 3], mesh.Vertices[mesh.Faces[i] * 3 + 1], mesh.Vertices[mesh.Faces[i] * 3 + 2], Conversions.GSACoincidentNodeAllowance).ToString());
      }
      ls.Add("0"); //Orientation node
      try
      {
        ls.Add(GSA.Get2DAngle(coor.ToArray(), mesh.Axis).ToString());
      }
      catch { ls.Add("0"); }
      ls.Add("NO_RLS");

      ls.Add("0"); // Offset x-start
      ls.Add("0"); // Offset x-end
      ls.Add("0"); // Offset y
      ls.Add(mesh.Offset.ToString());

      //ls.Add("NORMAL"); // Action // TODO: EL.4 SUPPORT
      ls.Add(mesh.GSADummy ? "DUMMY" : "");

      GSA.RunGWACommand(string.Join("\t", ls));
    }

  }

  [GSAObject("MEMB.7", new string[] { "NODE.2" }, "elements", false, true, new Type[] { typeof(GSANode), typeof(GSA2DProperty) }, new Type[] { typeof(GSA1DProperty) })]
  public class GSA2DMember : IGSASpeckleContainer
  {
    public int Group; // Keep for load targetting

    public int GSAId { get; set; }
    public string GWACommand { get; set; }
    public List<string> SubGWACommand { get; set; } = new List<string>();
    public dynamic Value { get; set; } = new Structural2DElementMesh();

    public void ParseGWACommand(GSAInterfacer GSA, List<GSANode> nodes, List<GSA2DProperty> props)
    {
      if (this.GWACommand == null)
        return;

      Structural2DElementMesh obj = new Structural2DElementMesh();

      string[] pieces = this.GWACommand.ListSplit("\t");

      int counter = 1; // Skip identifier
      this.GSAId = Convert.ToInt32(pieces[counter++]);
      obj.ApplicationId = GSA.GetSID(this.GetGSAKeyword(), this.GSAId);
      obj.Name = pieces[counter++].Trim(new char[] { '"' });
      var color = pieces[counter++].ParseGSAColor();

      string type = pieces[counter++];
      if (type == "SLAB")
        obj.ElementType = Structural2DElementType.Slab;
      else if (type == "WALL")
        obj.ElementType = Structural2DElementType.Wall;
      else
        obj.ElementType = Structural2DElementType.Generic;

      var propertyGSAId = Convert.ToInt32(pieces[counter++]);
      obj.PropertyRef = GSA.GetSID(typeof(GSA2DProperty).GetGSAKeyword(), propertyGSAId);
      this.Group = Convert.ToInt32(pieces[counter++]); // Keep group for load targetting

      List<double> coordinates = new List<double>();
      string[] nodeRefs = pieces[counter++].ListSplit(" ");
      for (int i = 0; i < nodeRefs.Length; i++)
      {
        GSANode node = nodes.Where(n => n.GSAId.ToString() == nodeRefs[i]).FirstOrDefault();
        coordinates.AddRange(node.Value.Value);
        this.SubGWACommand.Add(node.GWACommand);
      }

      Structural2DElementMesh temp = new Structural2DElementMesh(
          coordinates.ToArray(),
          color.HexToArgbColor(),
          obj.ElementType, obj.PropertyRef,
          null,
          null);

      obj.Vertices = temp.Vertices;
      obj.Faces = temp.Faces;
      obj.Colors = temp.Colors;

      int numFaces = 0;
      for (int i = 0; i < obj.Faces.Count(); i++)
      {
        numFaces++;
        i += obj.Faces[i] + 3;
      }

      counter++; // Orientation node

      GSA2DProperty prop = props.Where(p => p.Value.ApplicationId == obj.PropertyRef).FirstOrDefault();
      var axis = GSA.Parse2DAxis(coordinates.ToArray(),
          Convert.ToDouble(pieces[counter++]),
          prop == null ? false : (prop as GSA2DProperty).IsAxisLocal);
      obj.Axis = Enumerable.Repeat(axis, numFaces).ToList();
      if (prop != null)
        this.SubGWACommand.Add(prop.GWACommand);

      // Skip to offsets at second to last
      counter = pieces.Length - 2;

      var (offset, offsetRec) = GSA.GetGSATotal2DElementOffset(propertyGSAId, Convert.ToDouble(pieces[counter++]));
      this.SubGWACommand.Add(offsetRec);

      obj.Offset = Enumerable.Repeat(offset, numFaces).ToList();

      this.Value = obj;
    }

    public void SetGWACommand(GSAInterfacer GSA, int group = 0)
    {
      if (this.Value == null)
        return;

      Structural2DElementMesh mesh = this.Value as Structural2DElementMesh;

      string keyword = typeof(GSA2DMember).GetGSAKeyword();

      int index = GSA.Indexer.ResolveIndex(typeof(GSA2DMember), mesh);
      int propRef = 0;
      try
      {
        propRef = GSA.Indexer.LookupIndex(typeof(GSA2DProperty), mesh.PropertyRef).Value;
      }
      catch { }

      List<string> ls = new List<string>();

      ls.Add("SET");
      ls.Add(keyword + ":" + GSA.GenerateSID(mesh));
      ls.Add(index.ToString());
      ls.Add(mesh.Name == null || mesh.Name == "" ? " " : mesh.Name);
      ls.Add(mesh.Colors == null || mesh.Colors.Count() < 1 ? "NO_RGB" : mesh.Colors[0].ArgbToHexColor().ToString());
      if (mesh.ElementType == Structural2DElementType.Slab)
        ls.Add("SLAB");
      else if (mesh.ElementType == Structural2DElementType.Wall)
        ls.Add("WALL");
      else
        ls.Add("2D_GENERIC");
      ls.Add(propRef.ToString());
      ls.Add(group != 0 ? group.ToString() : index.ToString()); // TODO: This allows for targeting of elements from members group
      string topo = "";
      List<int[]> connectivities = mesh.Edges();
      List<double> coor = new List<double>();
      foreach (int[] conn in connectivities)
        foreach (int c in conn)
        {
          coor.AddRange(mesh.Vertices.Skip(c * 3).Take(3));
          topo += GSA.NodeAt(mesh.Vertices[c * 3], mesh.Vertices[c * 3 + 1], mesh.Vertices[c * 3 + 2], Conversions.GSACoincidentNodeAllowance).ToString() + " ";
        }
      ls.Add(topo);
      ls.Add("0"); // Orientation node
      try
      {
        ls.Add(GSA.Get2DAngle(coor.ToArray(), mesh.Axis.First()).ToString());
      }
      catch { ls.Add("0"); }
      ls.Add(mesh.GSAMeshSize == 0 ? "1" : mesh.GSAMeshSize.ToString()); // Target mesh size
      ls.Add("MESH"); // TODO: What is this?
      ls.Add("LINEAR"); // Element type
      ls.Add("0"); // Fire
      ls.Add("0"); // Time 1
      ls.Add("0"); // Time 2
      ls.Add("0"); // Time 3
      ls.Add("0"); // TODO: What is this?
      ls.Add(mesh.GSADummy ? "DUMMY" : "ACTIVE");
      ls.Add("NO"); // Internal auto offset
      ls.Add(mesh.Offset != null ? mesh.Offset.First().ToString() : "0"); // Offset z
      ls.Add("ALL"); // Exposure

      GSA.RunGWACommand(string.Join("\t", ls));
    }
  }
  
  public static partial class Conversions
  {
    public static bool ToNative(this Structural2DElement mesh)
    {
      if (Conversions.GSATargetLayer == GSATargetLayer.Analysis)
        new GSA2DElement() { Value = mesh }.SetGWACommand(GSA);
      else if (Conversions.GSATargetLayer == GSATargetLayer.Design)
        new GSA2DMember() { Value = mesh }.SetGWACommand(GSA);

      return true;
    }

    public static SpeckleObject ToSpeckle(this GSA2DElement dummyObject)
    {
      if (!GSASenderObjects.ContainsKey(typeof(GSA2DElement)))
        GSASenderObjects[typeof(GSA2DElement)] = new List<object>();

      if (!GSASenderObjects.ContainsKey(typeof(GSA2DElementMesh)))
        GSASenderObjects[typeof(GSA2DElementMesh)] = new List<object>();

      List<GSA2DElement> elements = new List<GSA2DElement>();
      List<GSANode> nodes = GSASenderObjects[typeof(GSANode)].Cast<GSANode>().ToList();
      List<GSA2DProperty> props = GSASenderObjects[typeof(GSA2DProperty)].Cast<GSA2DProperty>().ToList();

      string keyword = typeof(GSA2DElement).GetGSAKeyword();
      string[] subKeywords = typeof(GSA2DElement).GetSubGSAKeyword();

      string[] lines = GSA.GetGWARecords("GET_ALL\t" + keyword);
      List<string> deletedLines = GSA.GetDeletedGWARecords("GET_ALL\t" + keyword).ToList();
      foreach (string k in subKeywords)
        deletedLines.AddRange(GSA.GetDeletedGWARecords("GET_ALL\t" + k));

      // Remove deleted lines
      GSASenderObjects[typeof(GSA2DElement)].RemoveAll(l => deletedLines.Contains((l as IGSASpeckleContainer).GWACommand));
      foreach (KeyValuePair<Type, List<object>> kvp in GSASenderObjects)
        kvp.Value.RemoveAll(l => (l as IGSASpeckleContainer).SubGWACommand.Any(x => deletedLines.Contains(x)));

      // Filter only new lines
      string[] prevLines = GSASenderObjects[typeof(GSA2DElement)]
        .Select(l => (l as IGSASpeckleContainer).GWACommand)
        .Concat(GSASenderObjects[typeof(GSA2DElementMesh)].SelectMany(l => (l as IGSASpeckleContainer).SubGWACommand))
        .ToArray();
      string[] newLines = lines.Where(l => !prevLines.Contains(l)).ToArray();

      foreach (string p in newLines)
      {
        string[] pPieces = p.ListSplit("\t");
        if (pPieces[4].ParseElementNumNodes() == 3 | pPieces[4].ParseElementNumNodes() == 4)
        {
          GSA2DElement element = new GSA2DElement() { GWACommand = p };
          element.ParseGWACommand(GSA, nodes, props);
          elements.Add(element);
        }
      }

      GSASenderObjects[typeof(GSA2DElement)].AddRange(elements);

      if (elements.Count() == 0 && deletedLines.Count() == 0) return new SpeckleNull();

      return new SpeckleObject();
    }

    public static SpeckleObject ToSpeckle(this GSA2DMember dummyObject)
    {
      if (!GSASenderObjects.ContainsKey(typeof(GSA2DMember)))
        GSASenderObjects[typeof(GSA2DMember)] = new List<object>();

      List<GSA2DMember> members = new List<GSA2DMember>();
      List<GSANode> nodes = GSASenderObjects[typeof(GSANode)].Cast<GSANode>().ToList();
      List<GSA2DProperty> props = GSASenderObjects[typeof(GSA2DProperty)].Cast<GSA2DProperty>().ToList();

      string keyword = typeof(GSA2DMember).GetGSAKeyword();
      string[] subKeywords = typeof(GSA2DMember).GetSubGSAKeyword();

      string[] lines = GSA.GetGWARecords("GET_ALL\t" + keyword);
      List<string> deletedLines = GSA.GetDeletedGWARecords("GET_ALL\t" + keyword).ToList();
      foreach (string k in subKeywords)
        deletedLines.AddRange(GSA.GetDeletedGWARecords("GET_ALL\t" + k));

      // Remove deleted lines
      GSASenderObjects[typeof(GSA2DMember)].RemoveAll(l => deletedLines.Contains((l as IGSASpeckleContainer).GWACommand));
      foreach (KeyValuePair<Type, List<object>> kvp in GSASenderObjects)
        kvp.Value.RemoveAll(l => (l as IGSASpeckleContainer).SubGWACommand.Any(x => deletedLines.Contains(x)));

      // Filter only new lines
      string[] prevLines = GSASenderObjects[typeof(GSA2DMember)].Select(l => (l as IGSASpeckleContainer).GWACommand).ToArray();
      string[] newLines = lines.Where(l => !prevLines.Contains(l)).ToArray();

      foreach (string p in newLines)
      {
        string[] pPieces = p.ListSplit("\t");
        if (pPieces[4].MemberIs2D())
        {
          // Check if dummy
          if (pPieces[pPieces.Length - 4] == "ACTIVE")
          {
            GSA2DMember member = new GSA2DMember() { GWACommand = p };
            member.ParseGWACommand(GSA, nodes, props);
            members.Add(member);
          }
        }
      }

      GSASenderObjects[typeof(GSA2DMember)].AddRange(members);

      if (members.Count() > 0 || deletedLines.Count() > 0) return new SpeckleObject();

      return new SpeckleNull();
    }
  }
}