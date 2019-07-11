﻿using System;
using System.Collections.Generic;
using System.Linq;
using SpeckleCore;
using SpeckleElements;

namespace SpeckleElementsGSA
{
  [GSAObject("LOAD_2D_THERMAL.2", new string[] { }, "loads", true, true, new Type[] { typeof(GSA2DElement), typeof(GSA2DMember), typeof(GSALoadCase) }, new Type[] { typeof(GSA2DElement), typeof(GSA2DMember), typeof(GSALoadCase) })]
  public class GSA2DThermalLoading : IGSASpeckleContainer
  {
    public int GSAId { get; set; }
    public string GWACommand { get; set; }
    public List<string> SubGWACommand { get; set; } = new List<string>();
    public dynamic Value { get; set; } = new Structural2DThermalLoad();

    public void ParseGWACommand(GSAInterfacer GSA, List<GSA2DElement> e2Ds, List<GSA2DMember> m2Ds)
    {
      if (this.GWACommand == null)
        return;

      Structural2DThermalLoad obj = new Structural2DThermalLoad();

      string[] pieces = this.GWACommand.ListSplit("\t");

      int counter = 1; // Skip identifier
      
      obj.Name = pieces[counter++];

      var elementList = pieces[counter++];

      obj.ElementRefs = new List<string>();

      if (Conversions.GSATargetLayer == GSATargetLayer.Analysis)
      {
        var elementId = GSA.ConvertGSAList(elementList, GSAEntity.ELEMENT);
        foreach (int id in elementId)
        {
          IGSASpeckleContainer elem = e2Ds.Where(e => e.GSAId == id).FirstOrDefault();

          if (elem == null)
            continue;

          obj.ElementRefs.Add((elem.Value as SpeckleObject).ApplicationId);
          this.SubGWACommand.Add(elem.GWACommand);
        }
      }
      else
      {
        var groupIds = GSA.GetGroupsFromGSAList(elementList).ToList();
        foreach (int id in groupIds)
        {
          var memb2Ds = m2Ds.Where(m => m.Group == id);

          obj.ElementRefs.AddRange(memb2Ds.Select(m => (string)m.Value.ApplicationId));
          this.SubGWACommand.AddRange(memb2Ds.Select(m => m.GWACommand));
        }
      }

      obj.LoadCaseRef = GSA.GetSID(typeof(GSALoadCase).GetGSAKeyword(), Convert.ToInt32(pieces[counter++]));

      var loadingType = pieces[counter++];

      switch (loadingType)
      {
        case "CONS":
          obj.TopTemperature = Convert.ToDouble(pieces[counter++]);
          obj.BottomTemperature = obj.TopTemperature;
          break;
        case "DZ":
          obj.TopTemperature = Convert.ToDouble(pieces[counter++]);
          obj.BottomTemperature = Convert.ToDouble(pieces[counter++]);
          break;
        case "GEN":
          // GENERALIZE THIS TO AN AVERAGE
          for (int i = 0; i < 3; i++)
          { 
            obj.TopTemperature += Convert.ToDouble(pieces[counter++]);
            obj.BottomTemperature += Convert.ToDouble(pieces[counter++]);
          }
          obj.TopTemperature /= 4;
          obj.BottomTemperature /= 4;
          break;
      }

      this.Value = obj;
    }

    public void SetGWACommand(GSAInterfacer GSA)
    {
      if (this.Value == null)
        return;

      Structural2DThermalLoad loading = this.Value as Structural2DThermalLoad;

      string keyword = typeof(GSA2DThermalLoading).GetGSAKeyword();

      int index = GSA.Indexer.ResolveIndex(typeof(GSA2DThermalLoading), loading);
      
      var targetString = " ";

      if (loading.ElementRefs != null && loading.ElementRefs.Count() > 0)
      {
        if (Conversions.GSATargetLayer == GSATargetLayer.Analysis)
        {
          var e2DIndices = GSA.Indexer.LookupIndices(typeof(GSA2DElement), loading.ElementRefs).Where(x => x.HasValue).Select(x => x.Value).ToList();
          var e2DMeshIndices = GSA.Indexer.LookupIndices(typeof(GSA2DElementMesh), loading.ElementRefs).Where(x => x.HasValue).Select(x => x.Value).ToList();
          targetString = string.Join(" ",
            e2DIndices.Select(x => x.ToString())
            .Concat(e2DMeshIndices.Select(x => "G" + x.ToString()))
          );
        }
        else if (Conversions.GSATargetLayer == GSATargetLayer.Design)
        {
          var m2DIndices = GSA.Indexer.LookupIndices(typeof(GSA2DMember), loading.ElementRefs).Where(x => x.HasValue).Select(x => x.Value).ToList();
          targetString = string.Join(" ",
            m2DIndices.Select(x => "G" + x.ToString()));
        }
      }

      int? loadCaseRef = GSA.Indexer.LookupIndex(typeof(GSALoadCase), loading.LoadCaseRef);

      var loadingName = string.IsNullOrEmpty(loading.Name) ? " " : loading.Name;

      List<string> ls = new List<string>
        {
          "SET_AT",
          index.ToString(),
          keyword + ":" + GSA.GenerateSID(loading),
          loadingName, // Name
          targetString, //Elements
					(loadCaseRef.HasValue) ? loadCaseRef.Value.ToString() : "1",
        };

      if (loading.TopTemperature == loading.BottomTemperature)
      {
        ls.Add("CONS");
        ls.Add(loading.TopTemperature.ToString());
      }
      else
      {
        ls.Add("DZ");
        ls.Add(loading.TopTemperature.ToString());
        ls.Add(loading.BottomTemperature.ToString());
      }

      GSA.RunGWACommand(string.Join("\t", ls));
    }
  }

  public static partial class Conversions
  {
    public static bool ToNative(this Structural2DThermalLoad load)
    {
      var GSA2DElementLoadingThermal = new GSA2DThermalLoading() { Value = load };

      GSA2DElementLoadingThermal.SetGWACommand(GSA);

      return true;
    }

    public static SpeckleObject ToSpeckle(this GSA2DThermalLoading dummyObject)
    {
      if (!GSASenderObjects.ContainsKey(typeof(GSA2DThermalLoading)))
        GSASenderObjects[typeof(GSA2DThermalLoading)] = new List<object>();

      List<GSA2DThermalLoading> loads = new List<GSA2DThermalLoading>();
      var e2Ds = new List<GSA2DElement>();
      var m2Ds = new List<GSA2DMember>();

      if (Conversions.GSATargetLayer == GSATargetLayer.Analysis)
      {
        e2Ds = GSASenderObjects[typeof(GSA2DElement)].Cast<GSA2DElement>().ToList();
      }
      else if (Conversions.GSATargetLayer == GSATargetLayer.Design)
      {
        m2Ds = GSASenderObjects[typeof(GSA2DMember)].Cast<GSA2DMember>().ToList();
      }

      string keyword = typeof(GSA2DThermalLoading).GetGSAKeyword();
      string[] subKeywords = typeof(GSA2DThermalLoading).GetSubGSAKeyword();

      string[] lines = GSA.GetGWARecords("GET_ALL\t" + keyword);
      List<string> deletedLines = GSA.GetDeletedGWARecords("GET_ALL\t" + keyword).ToList();
      foreach (string k in subKeywords)
        deletedLines.AddRange(GSA.GetDeletedGWARecords("GET_ALL\t" + k));

      // Remove deleted lines
      GSASenderObjects[typeof(GSA2DThermalLoading)].RemoveAll(l => deletedLines.Contains((l as IGSASpeckleContainer).GWACommand));
      foreach (KeyValuePair<Type, List<object>> kvp in GSASenderObjects)
        kvp.Value.RemoveAll(l => (l as IGSASpeckleContainer).SubGWACommand.Any(x => deletedLines.Contains(x)));

      // Filter only new lines
      string[] prevLines = GSASenderObjects[typeof(GSA2DThermalLoading)].Select(l => (l as IGSASpeckleContainer).GWACommand).ToArray();
      string[] newLines = lines.Where(l => !prevLines.Contains(l)).ToArray();

      foreach (string p in newLines)
      {
        GSA2DThermalLoading load= new GSA2DThermalLoading() { GWACommand = p };
        load.ParseGWACommand(GSA, e2Ds, m2Ds);
        loads.Add(load);
      }

      GSASenderObjects[typeof(GSA2DThermalLoading)].AddRange(loads);

      if (loads.Count() > 0 || deletedLines.Count() > 0) return new SpeckleObject();

      return new SpeckleNull();
    }
  }
}