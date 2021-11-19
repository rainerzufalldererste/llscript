using System;
using System.Collections.Generic;

namespace llsc
{
  public class Scope
  {
    public SharedValue<long> maxRequiredStackSpace = new SharedValue<long>(0);

    public readonly Scope parentScope;
    public readonly bool isFunction;
    public readonly CFunction self;
    public int stackSpaceAllocationByteCodeIndex;
    public bool isConditional = false;

    private Dictionary<string, StructCType> definedStructs;
    private Dictionary<string, CFunction> definedFunctions;
    private Dictionary<string, CNamedValue> definedVariables;

    public LLI_Label_PseudoInstruction continueLabel, breakLabel, afterLabel;

    public List<CInstruction> instructions = new List<CInstruction>();

    /// <summary>
    /// Create Global Scope.
    /// </summary>
    public Scope()
    {
      parentScope = null;
      isFunction = false;
      self = null;
    }

    protected Scope(Scope parentScope, CFunction function)
    {
      this.parentScope = parentScope;
      this.isFunction = true;
      this.self = function;
      self.scope = this;
    }

    protected Scope(Scope parentScope)
    {
      this.parentScope = parentScope;
      this.isFunction = false;
      this.maxRequiredStackSpace = parentScope.maxRequiredStackSpace;
    }

    public void AddStruct(StructCType type)
    {
      if (definedStructs == null)
        definedStructs = new Dictionary<string, StructCType>();

      definedStructs.Add(type.name, type);
    }

    public void AddFunction(CFunction function)
    {
      if (definedFunctions == null)
        definedFunctions = new Dictionary<string, CFunction>();

      definedFunctions.Add(function.name, function);
    }

    public void AddVariable(CNamedValue value)
    {
      if (definedVariables == null)
        definedVariables = new Dictionary<string, CNamedValue>();

      if (value.type is VoidCType)
        Compiler.Error($"Identifier '{value.name}' cannot be of type '{value.type}'.", value.file, value.line);

      definedVariables.Add(value.name, value);
    }

    public StructCType GetStruct(string name)
    {
      if (definedStructs != null && definedStructs.ContainsKey(name))
        return definedStructs[name];

      if (parentScope != null)
        return parentScope.GetStruct(name);

      return null;
    }

    public CFunction GetFunction(string name)
    {
      if (definedFunctions != null && definedFunctions.ContainsKey(name))
        return definedFunctions[name];

      if (parentScope != null)
        return parentScope.GetFunction(name);

      return null;
    }

    public CNamedValue GetVariable(string name)
    {
      if (definedVariables != null && definedVariables.ContainsKey(name))
        return definedVariables[name];

      var parent = parentScope;
      bool functionLeft = false;

      while (parent != null)
      {
        functionLeft |= (parent.isFunction != isFunction);

        if (parent.definedVariables != null && parent.definedVariables.ContainsKey(name) && (!functionLeft || parent.definedVariables[name].isStatic))
          return parent.definedVariables[name];

        parent = parent.parentScope;
      }

      return null;
    }

    public IEnumerable<CFunction> GetLocalFunctions()
    {
      return this.definedFunctions == null ? (IEnumerable<CFunction>)new CFunction[0] : this.definedFunctions.Values;
    }

    public Scope GetChildScopeForFunction(CFunction function)
    {
      return new Scope(this, function);
    }

    public Scope GetChildScopeForConditional(LLI_Label_PseudoInstruction afterLabel)
    {
      return new Scope(this) { afterLabel = afterLabel };
    }

    public CFunction GetCurrentFunction()
    {
      if (self != null)
        return self;

      if (parentScope == null)
        return null;

      return parentScope.GetCurrentFunction();
    }

    public LLI_Label_PseudoInstruction GetBreakLabel()
    {
      if (breakLabel != null)
        return breakLabel;

      if (parentScope == null)
        return null;

      return parentScope.GetBreakLabel();
    }

    public LLI_Label_PseudoInstruction GetContinueLabel()
    {
      if (continueLabel != null)
        return continueLabel;

      if (parentScope == null)
        return null;

      return parentScope.GetContinueLabel();
    }
  }
}
