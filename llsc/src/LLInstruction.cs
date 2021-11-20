using System;
using System.Collections.Generic;

namespace llsc
{
  public enum ByteCodeInstructions
  {
    LLS_OP_EXIT = 0,

    LLS_OP_MOV_IMM_REGISTER,
    LLS_OP_MOV_REGISTER_REGISTER,
    LLS_OP_MOV_REGISTER_STACK,
    LLS_OP_MOV_STACK_REGISTER,
    LLS_OP_MOV_STACK_STACK,
    LLS_OP_MOV_REGISTER__PTR_IN_REGISTER,
    LLS_OP_MOV_PTR_IN_REGISTER__REGISTER,

    LLS_OP_MOV_REGISTER_STACK_N_BYTES,
    LLS_OP_MOV_STACK_STACK_N_BYTES,
    LLS_OP_MOV_REGISTER__PTR_IN_REGISTER_N_BYTES,

    LLS_OP_LEA_STACK_TO_REGISTER,

    LLS_OP_PUSH_REGISTER,
    LLS_OP_POP_REGISTER,

    LLS_OP_STACK_INC_IMM,
    LLS_OP_STACK_INC_REGISTER,
    LLS_OP_STACK_DEC_IMM,
    LLS_OP_STACK_DEC_REGISTER,

    LLS_OP_ADD_IMM,
    LLS_OP_ADD_REGISTER,
    LLS_OP_MULI_IMM,
    LLS_OP_MULI_REGISTER,
    LLS_OP_DIVI_IMM,
    LLS_OP_DIVI_REGISTER,
    LLS_OP_MULU_IMM,
    LLS_OP_MULU_REGISTER,
    LLS_OP_DIVU_IMM,
    LLS_OP_DIVU_REGISTER,
    LLS_OP_MOD_IMM,
    LLS_OP_MOD_REGISTER,

    LLS_OP_BSL_REGISTER,
    LLS_OP_BSR_REGISTER,
    LLS_OP_AND_REGISTER,
    LLS_OP_AND_IMM,
    LLS_OP_OR_REGISTER,
    LLS_OP_XOR_REGISTER,

    LLS_OP_LOGICAL_AND_REGISTER,
    LLS_OP_LOGICAL_OR_REGISTER,

    LLS_OP_INV_REGISTER,
    LLS_OP_NOT_REGISTER,

    LLS_OP_EQ_REGISTER,
    LLS_OP_LT_REGISTER,
    LLS_OP_GT_REGISTER,

    LLS_OP_NEGATE_REGISTER,

    LLS_OP_CMP_NEQ_IMM_REGISTER,

    LLS_OP_JMP,
    LLS_OP_JMP_RELATIVE_IMM,
    LLS_OP_JMP_RELATIVE_REGISTER,

    LLS_OP_JUMP_CMP_TRUE,
    LLS_OP_JUMP_CMP_TRUE_RELATIVE_IMM,
    LLS_OP_JUMP_CMP_TRUE_RELATIVE_REGISTER,

    LLS_OP_CALL_INTERNAL_RELATIVE_IMM,
    LLS_OP_CALL_INTERNAL_RELATIVE_REGISTER,
    LLS_OP_RETURN_INTERNAL,

    LLS_OP_CALL_EXTERNAL__RESULT_TO_REGISTER,

    LLS_OP_CALL_BUILTIN__RESULT_TO_REGISTER__ID_FROM_REGISTER,

    LLS_OP_MOV_RUNTIME_PARAM_REGISTER,
  };

  public abstract class LLInstruction
  {
    public static string currentFile;
    public static int currentLine;

    public string file;
    public int line;

    public ulong position;

    public ulong bytecodeSize { get; protected set; }

    public LLInstruction(ulong bytecodeSize)
    {
      this.file = currentFile;
      this.line = currentLine;

      this.bytecodeSize = bytecodeSize;
    }

    /// <summary>
    /// Should not be called if bytecodeSize == 0;
    /// </summary>
    /// <param name="byteCode">the byte code to append to.</param>
    public abstract void AppendBytecode(ref List<byte> byteCode);

    public virtual void AfterCodeGen() { }
  }
  public class LLI_Exit : LLInstruction
  {
    public LLI_Exit() : base(1) { }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_EXIT);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_EXIT}";
  }

  public abstract class LLI_PseudoInstruction : LLInstruction
  {
    public LLI_PseudoInstruction(ulong size) : base(size) { }
  }

  public class LLI_Label_PseudoInstruction : LLI_PseudoInstruction
  {
    public string description;

    public LLI_Label_PseudoInstruction(string description) : base(0)
    {
      this.description = description;
    }

    public override void AppendBytecode(ref List<byte> byteCode) { }

    public override string ToString() => $"label_0x{GetHashCode():X}_at_0x{position:X}: ('{description}')";
  }

  public class LLI_Comment_PseudoInstruction : LLI_PseudoInstruction
  {
    public string comment;

    public LLI_Comment_PseudoInstruction(string comment) : base(0)
    {
      this.comment = comment;
    }

    public override void AppendBytecode(ref List<byte> byteCode) { }

    public override string ToString() => $"# Comment: {comment}";
  }

  public class LLI_Location_PseudoInstruction : LLI_PseudoInstruction
  {
    public DbgLocationInfo locationInfo;

    public LLI_Location_PseudoInstruction(CValue value, SharedValue<long> stackSize, ByteCodeState byteCodeState) : base(0)
    {
      if (!value.hasPosition)
        throw new Exception($"Internal Compiler Error! Location Pseudo Instruction is invalid, since the value {value} doesn't claim having a position.");

      locationInfo = new DbgLocationInfo(value, stackSize);

      if (value.position.type == PositionType.InRegister && byteCodeState.registers[value.position.registerIndex] != value)
        throw new Exception("Internal Compiler Error: Value not available in specified register.");
    }

    public override void AppendBytecode(ref List<byte> byteCode) { }

    public override string ToString()
    {
      string ret = $"# Value Location: '{(!locationInfo.isVariable ? "(" : "") + locationInfo.name + (!locationInfo.isVariable ? ")" : "")}' ";

      switch (locationInfo.position.type)
      {
        case PositionType.OnStack:
          return ret + $"s:{locationInfo.stackSize.Value - locationInfo.position.stackOffsetForward}";

        default:
          return ret + locationInfo.position.ToString();
      }
    }
  }

  public class LLI_Data_PseudoInstruction : LLI_PseudoInstruction
  {
    public string description;
    public byte[] data;

    public LLI_Data_PseudoInstruction(string description, byte[] data) : base((ulong)data.LongLength)
    {
      this.description = description;
      this.data = data;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.AddRange(data);
    }

    public override string ToString()
    {
      string ret = $"# Data Segment: '{description}'\n";

      long i = 0;

      for (; i < data.LongLength - 16; i += 16)
      {
        for (long j = 0; j < 16; j++)
          ret += data[i + j].ToString("X2") + " ";

        ret += "\n";
      }

      if (i < data.LongLength)
        for (; i < data.LongLength; i++)
          ret += data[i].ToString("X2") + " ";
     
      return ret;
    }
  }

  public class LLI_JumpToLabel : LLInstruction
  {
    readonly LLI_Label_PseudoInstruction label;

    public LLI_JumpToLabel(LLI_Label_PseudoInstruction label) : base(9) => this.label = label;

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_JMP_RELATIVE_IMM);
      byteCode.AddRange(BitConverter.GetBytes((long)label.position - (long)(this.position + this.bytecodeSize)));
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_JMP_RELATIVE_IMM} label_0x{label.GetHashCode():X}_at_0x{label.position:X}";
  }

  public class LLI_StackIncrementImm : LLInstruction
  {
    readonly SharedValue<long> value;
    long offset = 0;

    public LLI_StackIncrementImm(long value) : base(9)
    {
      this.value = new SharedValue<long>(value);
    }

    public LLI_StackIncrementImm(SharedValue<long> value, long offset = 0) : base(9)
    {
      this.value = value;
      this.offset = offset;
    }

    public override void AfterCodeGen()
    {
      if (((long)this.value.Value + offset) == 0)
        base.bytecodeSize = 0;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_STACK_INC_IMM);
      byteCode.AddRange(BitConverter.GetBytes(value.Value - offset));
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_STACK_INC_IMM} {value.Value - offset}";
  }

  public class LLI_StackDecrementImm : LLInstruction
  {
    readonly SharedValue<long> value;
    long offset = 0;

    public LLI_StackDecrementImm(long value) : base(9)
    {
      this.value = new SharedValue<long>(value);
    }

    public LLI_StackDecrementImm(SharedValue<long> value, long offset = 0) : base(9)
    {
      this.value = value;
      this.offset = offset;
    }

    public override void AfterCodeGen()
    {
      if (((long)this.value.Value + offset) == 0)
        base.bytecodeSize = 0;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_STACK_DEC_IMM);
      byteCode.AddRange(BitConverter.GetBytes(value.Value - offset));
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_STACK_DEC_IMM} {value.Value - offset}";
  }

  public class LLI_LoadEffectiveAddress_StackOffsetToRegister : LLInstruction
  {
    readonly SharedValue<long> value;
    readonly SharedValue<long> offset;
    readonly int register;

    public LLI_LoadEffectiveAddress_StackOffsetToRegister(long value, long offset, int register) : base(10)
    {
      this.value = new SharedValue<long>(value);
      this.offset = new SharedValue<long>(offset);
      this.register = register;
    }

    public LLI_LoadEffectiveAddress_StackOffsetToRegister(SharedValue<long> value, long offset, int register) : base(10)
    {
      this.value = value;
      this.offset = new SharedValue<long>(offset);
      this.register = register;
    }

    public LLI_LoadEffectiveAddress_StackOffsetToRegister(SharedValue<long> value, SharedValue<long> offset, int register) : base(10)
    {
      this.value = value;
      this.offset = offset;
      this.register = register;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_LEA_STACK_TO_REGISTER);
      byteCode.Add((byte)register);
      byteCode.AddRange(BitConverter.GetBytes(value.Value - offset.Value));
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_LEA_STACK_TO_REGISTER} r:{register}, {value.Value - offset.Value}";
  }

  public class LLI_MovRegisterToRegister : LLInstruction
  {
    readonly int sourceRegister, targetRegister;

    public LLI_MovRegisterToRegister(int sourceRegister, int targetRegister) : base(3)
    {
      this.sourceRegister = sourceRegister;
      this.targetRegister = targetRegister;
    }

    public override void AfterCodeGen()
    {
      if (sourceRegister == targetRegister)
        this.bytecodeSize = 0;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_MOV_REGISTER_REGISTER);
      byteCode.Add((byte)targetRegister);
      byteCode.Add((byte)sourceRegister);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_MOV_REGISTER_REGISTER} r:{targetRegister}, r:{sourceRegister}";
  }

  public class LLI_PushRegister : LLInstruction
  {
    readonly byte register;

    public LLI_PushRegister(byte register) : base(2)
    {
      this.register = register;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_PUSH_REGISTER);
      byteCode.Add(register);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_PUSH_REGISTER} r:{register}";
  }

  public class LLI_PopRegister : LLInstruction
  {
    readonly byte register;

    public LLI_PopRegister(byte register) : base(2)
    {
      this.register = register;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_POP_REGISTER);
      byteCode.Add(register);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_POP_REGISTER} r:{register}";
  }

  public class LLI_MovRegisterToStackOffset : LLInstruction
  {
    readonly int register;
    readonly SharedValue<long> stackSize;
    readonly long offset;

    public LLI_MovRegisterToStackOffset(int register, SharedValue<long> stackSize, long offset) : base(1 + 8 + 1)
    {
      this.register = register;
      this.stackSize = stackSize;
      this.offset = offset;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_MOV_REGISTER_STACK);
      byteCode.AddRange(BitConverter.GetBytes(stackSize.Value - offset));
      byteCode.Add((byte)register);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_MOV_REGISTER_STACK} {stackSize.Value - offset}, r:{register}";
  }

  public class LLI_MovRegisterToStackOffset_NBytes : LLInstruction
  {
    readonly int register;
    readonly SharedValue<long> stackSize;
    readonly long offset;
    readonly int bytes;

    public LLI_MovRegisterToStackOffset_NBytes(int register, SharedValue<long> stackSize, long offset, int bytes) : base(1 + 8 + 1 + 1)
    {
      this.register = register;
      this.stackSize = stackSize;
      this.offset = offset;
      this.bytes = bytes;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_MOV_REGISTER_STACK_N_BYTES);
      byteCode.AddRange(BitConverter.GetBytes(stackSize.Value - offset));
      byteCode.Add((byte)register);
      byteCode.Add((byte)bytes);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_MOV_REGISTER_STACK_N_BYTES} {stackSize.Value - offset}, r:{register}, {bytes}";
  }

  public class LLI_MovStackOffsetToRegister : LLInstruction
  {
    readonly int register;
    readonly SharedValue<long> stackSize;
    readonly long offset;

    public LLI_MovStackOffsetToRegister(SharedValue<long> stackSize, long offset, int register) : base(1 + 8 + 1)
    {
      this.register = register;
      this.stackSize = stackSize;
      this.offset = offset;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_MOV_STACK_REGISTER);
      byteCode.Add((byte)register);
      byteCode.AddRange(BitConverter.GetBytes(stackSize.Value - offset));
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_MOV_STACK_REGISTER} r:{register}, {stackSize.Value - offset}";
  }

  public class LLI_MovStackOffsetToStackOffset : LLInstruction
  {
    SharedValue<long> sourceStackSize;
    long sourceOffset;

    SharedValue<long> targetStackSize;
    long targetOffset;

    public LLI_MovStackOffsetToStackOffset(SharedValue<long> sourceStackSize, long sourceOffset, SharedValue<long> targetStackSize, long targetOffset) : base(1 + 8 + 8)
    {
      this.sourceStackSize = sourceStackSize;
      this.sourceOffset = sourceOffset;

      this.targetStackSize = targetStackSize;
      this.targetOffset = targetOffset;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_MOV_STACK_STACK);
      byteCode.AddRange(BitConverter.GetBytes(targetStackSize.Value - targetOffset));
      byteCode.AddRange(BitConverter.GetBytes(sourceStackSize.Value - sourceOffset));
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_MOV_STACK_STACK} {targetStackSize.Value - targetOffset}, {sourceStackSize.Value - sourceOffset}";
  }

  public class LLI_MovStackOffsetToStackOffset_NBytes : LLInstruction
  {
    SharedValue<long> sourceStackSize;
    long sourceOffset;

    SharedValue<long> targetStackSize;
    long targetOffset;

    byte bytes;

    public LLI_MovStackOffsetToStackOffset_NBytes(SharedValue<long> sourceStackSize, long sourceOffset, SharedValue<long> targetStackSize, long targetOffset, byte bytes) : base(1 + 8 + 8 + 1)
    {
      this.sourceStackSize = sourceStackSize;
      this.sourceOffset = sourceOffset;

      this.targetStackSize = targetStackSize;
      this.targetOffset = targetOffset;

      this.bytes = bytes;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_MOV_STACK_STACK_N_BYTES);
      byteCode.AddRange(BitConverter.GetBytes(targetStackSize.Value - targetOffset));
      byteCode.AddRange(BitConverter.GetBytes(sourceStackSize.Value - sourceOffset));
      byteCode.Add(bytes);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_MOV_STACK_STACK_N_BYTES} {targetStackSize.Value - targetOffset}, {sourceStackSize.Value - sourceOffset}, {bytes}";
  }

  public class LLI_MovRegisterToPtrInRegister : LLInstruction
  {
    int targetPtrRegister, sourceValueRegister;

    public LLI_MovRegisterToPtrInRegister(int targetPtrRegister, int sourceValueRegister) : base(3)
    {
      this.targetPtrRegister = targetPtrRegister;
      this.sourceValueRegister = sourceValueRegister;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_MOV_REGISTER__PTR_IN_REGISTER);
      byteCode.Add((byte)targetPtrRegister);
      byteCode.Add((byte)sourceValueRegister);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_MOV_REGISTER__PTR_IN_REGISTER} r:{targetPtrRegister}, r:{sourceValueRegister}";
  }

  public class LLI_MovRegisterToPtrInRegister_NBytes : LLInstruction
  {
    int targetPtrRegister, sourceValueRegister, bytes;

    public LLI_MovRegisterToPtrInRegister_NBytes(int targetPtrRegister, int sourceValueRegister, int bytes) : base(4)
    {
      this.targetPtrRegister = targetPtrRegister;
      this.sourceValueRegister = sourceValueRegister;
      this.bytes = bytes;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_MOV_REGISTER__PTR_IN_REGISTER_N_BYTES);
      byteCode.Add((byte)targetPtrRegister);
      byteCode.Add((byte)sourceValueRegister);
      byteCode.Add((byte)bytes);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_MOV_REGISTER__PTR_IN_REGISTER_N_BYTES} r:{targetPtrRegister}, r:{sourceValueRegister}, {bytes}";
  }

  public class LLI_MovFromPtrInRegisterToRegister : LLInstruction
  {
    int sourcePtrRegister, targetRegister;

    public LLI_MovFromPtrInRegisterToRegister(int sourcePtrRegister, int targetRegister) : base(3)
    {
      this.sourcePtrRegister = sourcePtrRegister;
      this.targetRegister = targetRegister;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_MOV_PTR_IN_REGISTER__REGISTER);
      byteCode.Add((byte)targetRegister);
      byteCode.Add((byte)sourcePtrRegister);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_MOV_PTR_IN_REGISTER__REGISTER} r:{targetRegister}, r:{sourcePtrRegister}";
  }

  public class LLI_MovImmToRegister : LLInstruction
  {
    int register;
    byte[] value;

    public LLI_MovImmToRegister(int register, byte[] value) : base(1 + 1 + 8)
    {
      this.register = register;
      this.value = value;

      if (this.value.Length != 8)
        throw new Exception("Internal Compiler Error!");
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_MOV_IMM_REGISTER);
      byteCode.Add((byte)register);
      byteCode.AddRange(value);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_MOV_IMM_REGISTER} r:{register}, {value.ElementsToString("X2")}";
  }

  public class LLI_AddImm : LLInstruction
  {
    int register;
    byte[] value;

    public LLI_AddImm(int register, byte[] value) : base(1 + 1 + 8)
    {
      this.register = register;
      this.value = value;

      if (this.value.Length != 8)
        throw new Exception("Internal Compiler Error!");

      if (Compiler.OptimizationLevel != 0)
      {
        bool isZero = true;

        foreach (var x in value)
        {
          if (x != 0)
          {
            isZero = false;
            break;
          }
        }

        if (isZero)
          bytecodeSize = 0;
      }
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      if (bytecodeSize != 0)
      {
        byteCode.Add((byte)ByteCodeInstructions.LLS_OP_ADD_IMM);
        byteCode.Add((byte)register);
        byteCode.AddRange(value);
      }
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_ADD_IMM} r:{register}, {value.ElementsToString("X2")}";
  }

  public class LLI_AddImmInstructionOffset : LLInstruction
  {
    int register;
    LLInstruction instruction;

    public LLI_AddImmInstructionOffset(int register, LLInstruction instruction) : base(1 + 1 + 8)
    {
      this.register = register;
      this.instruction = instruction;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_ADD_IMM);
      byteCode.Add((byte)register);
      byteCode.AddRange(BitConverter.GetBytes(instruction.position));
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_ADD_IMM} r:{register}, {BitConverter.GetBytes(instruction.position).ElementsToString("X2")} (offset of instruction at 0x{instruction.position:X})";
  }

  public class LLI_AddRegister : LLInstruction
  {
    int src_dst;
    int operand;

    public LLI_AddRegister(int src_dst, int operand) : base(1 + 1 + 1)
    {
      this.src_dst = src_dst;
      this.operand = operand;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_ADD_REGISTER);
      byteCode.Add((byte)src_dst);
      byteCode.Add((byte)operand);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_ADD_REGISTER} r:{src_dst}, r:{operand}";
  }

  public class LLI_MultiplySignedRegister : LLInstruction
  {
    int src_dst;
    int operand;

    public LLI_MultiplySignedRegister(int src_dst, int operand) : base(1 + 1 + 1)
    {
      this.src_dst = src_dst;
      this.operand = operand;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_MULI_REGISTER);
      byteCode.Add((byte)src_dst);
      byteCode.Add((byte)operand);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_MULI_REGISTER} r:{src_dst}, r:{operand}";
  }

  public class LLI_MultiplySignedImm : LLInstruction
  {
    int register;
    byte[] value;

    public LLI_MultiplySignedImm(int register, byte[] value) : base(1 + 1 + 8)
    {
      this.register = register;
      this.value = value;

      if (this.value.Length != 8)
        throw new Exception("Internal Compiler Error!");
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_MULI_IMM);
      byteCode.Add((byte)register);
      byteCode.AddRange(value);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_MULI_IMM} r:{register}, {value.ElementsToString("X2")}";
  }

  public class LLI_MultiplyUnsignedRegister : LLInstruction
  {
    int src_dst;
    int operand;

    public LLI_MultiplyUnsignedRegister(int src_dst, int operand) : base(1 + 1 + 1)
    {
      this.src_dst = src_dst;
      this.operand = operand;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_MULU_REGISTER);
      byteCode.Add((byte)src_dst);
      byteCode.Add((byte)operand);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_MULU_REGISTER} r:{src_dst}, r:{operand}";
  }

  public class LLI_MultiplyUnsignedImm : LLInstruction
  {
    int register;
    byte[] value;

    public LLI_MultiplyUnsignedImm(int register, byte[] value) : base(1 + 1 + 8)
    {
      this.register = register;
      this.value = value;

      if (this.value.Length != 8)
        throw new Exception("Internal Compiler Error!");
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_MULU_IMM);
      byteCode.Add((byte)register);
      byteCode.AddRange(value);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_MULU_IMM} r:{register}, {value.ElementsToString("X2")}";
  }

  public class LLI_DivideSignedRegister : LLInstruction
  {
    int src_dst;
    int operand;

    public LLI_DivideSignedRegister(int src_dst, int operand) : base(1 + 1 + 1)
    {
      this.src_dst = src_dst;
      this.operand = operand;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_DIVI_REGISTER);
      byteCode.Add((byte)src_dst);
      byteCode.Add((byte)operand);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_DIVI_REGISTER} r:{src_dst}, r:{operand}";
  }

  public class LLI_DivideSignedImm : LLInstruction
  {
    int register;
    byte[] value;

    public LLI_DivideSignedImm(int register, byte[] value) : base(1 + 1 + 8)
    {
      this.register = register;
      this.value = value;

      if (this.value.Length != 8)
        throw new Exception("Internal Compiler Error!");
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_DIVI_IMM);
      byteCode.Add((byte)register);
      byteCode.AddRange(value);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_DIVI_IMM} r:{register}, {value.ElementsToString("X2")}";
  }

  public class LLI_DivideUnsignedRegister : LLInstruction
  {
    int src_dst;
    int operand;

    public LLI_DivideUnsignedRegister(int src_dst, int operand) : base(1 + 1 + 1)
    {
      this.src_dst = src_dst;
      this.operand = operand;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_DIVU_REGISTER);
      byteCode.Add((byte)src_dst);
      byteCode.Add((byte)operand);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_DIVU_REGISTER} r:{src_dst}, r:{operand}";
  }

  public class LLI_DivideUnsignedImm : LLInstruction
  {
    int register;
    byte[] value;

    public LLI_DivideUnsignedImm(int register, byte[] value) : base(1 + 1 + 8)
    {
      this.register = register;
      this.value = value;

      if (this.value.Length != 8)
        throw new Exception("Internal Compiler Error!");
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_DIVU_IMM);
      byteCode.Add((byte)register);
      byteCode.AddRange(value);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_DIVU_IMM} r:{register}, {value.ElementsToString("X2")}";
  }

  public class LLI_ModuloRegister : LLInstruction
  {
    int src_dst;
    int operand;

    public LLI_ModuloRegister(int src_dst, int operand) : base(1 + 1 + 1)
    {
      this.src_dst = src_dst;
      this.operand = operand;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_MOD_REGISTER);
      byteCode.Add((byte)src_dst);
      byteCode.Add((byte)operand);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_MOD_REGISTER} r:{src_dst}, r:{operand}";
  }

  public class LLI_ModuloImm : LLInstruction
  {
    int register;
    byte[] value;

    public LLI_ModuloImm(int register, byte[] value) : base(1 + 1 + 8)
    {
      this.register = register;
      this.value = value;

      if (this.value.Length != 8)
        throw new Exception("Internal Compiler Error!");
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_MOD_IMM);
      byteCode.Add((byte)register);
      byteCode.AddRange(value);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_MOD_IMM} r:{register}, {value.ElementsToString("X2")}";
  }

  public class LLI_AndImm : LLInstruction
  {
    int register;
    byte[] value;

    public LLI_AndImm(int register, byte[] value) : base(1 + 1 + 8)
    {
      this.register = register;
      this.value = value;

      if (this.value.Length != 8)
        throw new Exception("Internal Compiler Error!");
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_AND_IMM);
      byteCode.Add((byte)register);
      byteCode.AddRange(value);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_AND_IMM} r:{register}, {value.ElementsToString("X2")}";
  }

  public class LLI_AndRegister : LLInstruction
  {
    int src_dst;
    int operand;

    public LLI_AndRegister(int src_dst, int operand) : base(1 + 1 + 1)
    {
      this.src_dst = src_dst;
      this.operand = operand;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_AND_REGISTER);
      byteCode.Add((byte)src_dst);
      byteCode.Add((byte)operand);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_AND_REGISTER} r:{src_dst}, r:{operand}";
  }

  public class LLI_OrRegister : LLInstruction
  {
    int src_dst;
    int operand;

    public LLI_OrRegister(int src_dst, int operand) : base(1 + 1 + 1)
    {
      this.src_dst = src_dst;
      this.operand = operand;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_OR_REGISTER);
      byteCode.Add((byte)src_dst);
      byteCode.Add((byte)operand);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_OR_REGISTER} r:{src_dst}, r:{operand}";
  }

  public class LLI_XOrRegister : LLInstruction
  {
    int src_dst;
    int operand;

    public LLI_XOrRegister(int src_dst, int operand) : base(1 + 1 + 1)
    {
      this.src_dst = src_dst;
      this.operand = operand;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_XOR_REGISTER);
      byteCode.Add((byte)src_dst);
      byteCode.Add((byte)operand);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_XOR_REGISTER} r:{src_dst}, r:{operand}";
  }

  public class LLI_BitShiftLeftRegister : LLInstruction
  {
    int src_dst;
    int operand;

    public LLI_BitShiftLeftRegister(int src_dst, int operand) : base(1 + 1 + 1)
    {
      this.src_dst = src_dst;
      this.operand = operand;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_BSL_REGISTER);
      byteCode.Add((byte)src_dst);
      byteCode.Add((byte)operand);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_BSL_REGISTER} r:{src_dst}, r:{operand}";
  }

  public class LLI_BitShiftRightRegister : LLInstruction
  {
    int src_dst;
    int operand;

    public LLI_BitShiftRightRegister(int src_dst, int operand) : base(1 + 1 + 1)
    {
      this.src_dst = src_dst;
      this.operand = operand;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_BSR_REGISTER);
      byteCode.Add((byte)src_dst);
      byteCode.Add((byte)operand);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_BSR_REGISTER} r:{src_dst}, r:{operand}";
  }

  public class LLI_LogicalAndRegister : LLInstruction
  {
    int src_dst;
    int operand;

    public LLI_LogicalAndRegister(int src_dst, int operand) : base(1 + 1 + 1)
    {
      this.src_dst = src_dst;
      this.operand = operand;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_LOGICAL_AND_REGISTER);
      byteCode.Add((byte)src_dst);
      byteCode.Add((byte)operand);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_LOGICAL_AND_REGISTER} r:{src_dst}, r:{operand}";
  }

  public class LLI_LogicalOrRegister : LLInstruction
  {
    int src_dst;
    int operand;

    public LLI_LogicalOrRegister(int src_dst, int operand) : base(1 + 1 + 1)
    {
      this.src_dst = src_dst;
      this.operand = operand;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_LOGICAL_OR_REGISTER);
      byteCode.Add((byte)src_dst);
      byteCode.Add((byte)operand);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_LOGICAL_OR_REGISTER} r:{src_dst}, r:{operand}";
  }

  public class LLI_NegateRegister : LLInstruction
  {
    int register;

    public LLI_NegateRegister(int register) : base(1 + 1)
    {
      this.register = register;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_NEGATE_REGISTER);
      byteCode.Add((byte)register);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_NEGATE_REGISTER} r:{register}";
  }

  public class LLI_InverseRegister : LLInstruction
  {
    int register;

    public LLI_InverseRegister(int register) : base(1 + 1)
    {
      this.register = register;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_INV_REGISTER);
      byteCode.Add((byte)register);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_INV_REGISTER} r:{register}";
  }

  public class LLI_EqualsRegister : LLInstruction
  {
    int src_dst;
    int operand;

    public LLI_EqualsRegister(int src_dst, int operand) : base(1 + 1 + 1)
    {
      this.src_dst = src_dst;
      this.operand = operand;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_EQ_REGISTER);
      byteCode.Add((byte)src_dst);
      byteCode.Add((byte)operand);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_EQ_REGISTER} r:{src_dst}, r:{operand}";
  }

  public class LLI_LessThanRegister : LLInstruction
  {
    int src_dst;
    int operand;

    public LLI_LessThanRegister(int src_dst, int operand) : base(1 + 1 + 1)
    {
      this.src_dst = src_dst;
      this.operand = operand;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_LT_REGISTER);
      byteCode.Add((byte)src_dst);
      byteCode.Add((byte)operand);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_LT_REGISTER} r:{src_dst}, r:{operand}";
  }

  public class LLI_GreaterThanRegister : LLInstruction
  {
    int src_dst;
    int operand;

    public LLI_GreaterThanRegister(int src_dst, int operand) : base(1 + 1 + 1)
    {
      this.src_dst = src_dst;
      this.operand = operand;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_GT_REGISTER);
      byteCode.Add((byte)src_dst);
      byteCode.Add((byte)operand);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_GT_REGISTER} r:{src_dst}, r:{operand}";
  }

  public class LLI_NotRegister : LLInstruction
  {
    int register;

    public LLI_NotRegister(int register) : base(1 + 1)
    {
      this.register = register;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_NOT_REGISTER);
      byteCode.Add((byte)register);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_NOT_REGISTER} r:{register}";
  }

  public class LLI_CallBuiltInFunction_IDFromRegister_ResultToRegister : LLInstruction
  {
    byte idRegister;
    byte resultRegister;

    public LLI_CallBuiltInFunction_IDFromRegister_ResultToRegister(byte idRegister, byte resultRegister) : base(1 + 1 + 1)
    {
      this.idRegister = idRegister;
      this.resultRegister = resultRegister;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_CALL_BUILTIN__RESULT_TO_REGISTER__ID_FROM_REGISTER);
      byteCode.Add(idRegister);
      byteCode.Add(resultRegister);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_CALL_BUILTIN__RESULT_TO_REGISTER__ID_FROM_REGISTER} r:{idRegister}, r:{resultRegister}";
  }

  public class LLI_CallExternFunction_ResultToRegister : LLInstruction
  {
    byte resultRegister;

    public LLI_CallExternFunction_ResultToRegister(byte resultRegister) : base(1 + 1)
    {
      this.resultRegister = resultRegister;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_CALL_EXTERNAL__RESULT_TO_REGISTER);
      byteCode.Add(resultRegister);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_CALL_EXTERNAL__RESULT_TO_REGISTER} r:{resultRegister}";
  }

  public class LLI_CallFunctionAtRelativeImm : LLInstruction
  {
    LLI_Label_PseudoInstruction label;

    public LLI_CallFunctionAtRelativeImm(LLI_Label_PseudoInstruction label) : base(1 + 8)
    {
      this.label = label;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_CALL_INTERNAL_RELATIVE_IMM);
      byteCode.AddRange(BitConverter.GetBytes((long)label.position - (long)(this.position + this.bytecodeSize)));
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_CALL_INTERNAL_RELATIVE_IMM} label_0x{label.GetHashCode():X}_at_0x{label.position:X}";
  }

  public class LLI_Return : LLInstruction
  {
    public LLI_Return() : base(1) { }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_RETURN_INTERNAL);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_RETURN_INTERNAL}";
  }

  public class LLI_CmpNotEq_ImmRegister : LLInstruction
  {
    byte register;
    byte[] imm;

    public LLI_CmpNotEq_ImmRegister(byte[] imm, byte registerIndex) : base(1 + 1 + 8)
    {
      if (imm.Length != 8)
        throw new Exception("Internal Compiler Error!");

      this.imm = imm;
      this.register = registerIndex;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_CMP_NEQ_IMM_REGISTER);
      byteCode.Add(register);
      byteCode.AddRange(imm);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_CMP_NEQ_IMM_REGISTER} r:{register}, {imm.ElementsToString("X2")}";
  }

  public class LLI_JumpIfTrue_Imm : LLInstruction
  {
    LLI_Label_PseudoInstruction label;

    public LLI_JumpIfTrue_Imm(LLI_Label_PseudoInstruction label) : base(1 + 8)
    {
      this.label = label;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_JUMP_CMP_TRUE_RELATIVE_IMM);
      byteCode.AddRange(BitConverter.GetBytes((long)label.position - (long)(this.position + this.bytecodeSize)));
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_JUMP_CMP_TRUE_RELATIVE_IMM} label_0x{label.GetHashCode():X}_at_0x{label.position:X}";
  }

  public enum LLI_RuntimeParam
  {
    LLS_RP_CODE_BASE_PTR = 0,
    LLS_RP_CODE_INSTRUCTION_PTR,
    LLS_RP_STACK_BASE_PTR,
  }

  public class LLI_MovRuntimeParamToRegister : LLInstruction
  {
    LLI_RuntimeParam param;
    byte register;

    public LLI_MovRuntimeParamToRegister(LLI_RuntimeParam param, byte register) : base(1 + 1 + 1)
    {
      this.param = param;
      this.register = register;
    }

    public override void AppendBytecode(ref List<byte> byteCode)
    {
      byteCode.Add((byte)ByteCodeInstructions.LLS_OP_MOV_RUNTIME_PARAM_REGISTER);
      byteCode.Add((byte)param);
      byteCode.Add((byte)register);
    }

    public override string ToString() => $"{ByteCodeInstructions.LLS_OP_MOV_RUNTIME_PARAM_REGISTER} {(byte)param} ({param}), r:{register}";
  }
}
