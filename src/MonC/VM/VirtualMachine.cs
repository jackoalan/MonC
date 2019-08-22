using System;
using System.Collections.Generic;
using MonC.Bytecode;
using MonC.Codegen;

namespace MonC.VM
{
    public class VirtualMachine
    {
        private readonly Stack<StackFrame> _callStack = new Stack<StackFrame>();
        private readonly List<int> _argumentStack = new List<int>();
        private VMModule _module;
        private int _aRegister;
        private int _bRegister;

        private bool _isRunning;
        private bool _canContinue;

        public void LoadModule(VMModule module)
        {
            if (_isRunning) {
                throw new InvalidOperationException("Cannot load module while running");
            }
            _module = module;
        }

        public void Call(string functionName, IEnumerable<int> arguments)
        {
            if (_isRunning) {
                throw new InvalidOperationException("Cannot call function while running");
            }
            
            int functionIndex = LookupFunction(functionName);

            if (functionIndex == -1) {
                throw new ArgumentException(
                    message:   "No function by the given name was found in the loaded module",
                    paramName: nameof(functionName));
            }

            _isRunning = true;
            
            _argumentStack.AddRange(arguments);
            
            PushCall(functionIndex);
            Continue();
        }

        private int LookupFunction(string functionName)
        {
            for (int i = 0, ilen = _module.Module.ExportedFunctions.Length; i < ilen; ++i) {
                KeyValuePair<string, int> exportedFunction = _module.Module.ExportedFunctions[i];
                if (exportedFunction.Key == functionName) {
                    return exportedFunction.Value;
                }
            }
            return -1;
        }

        private void Continue()
        {
            _canContinue = true;

            while (_canContinue) {
                InterpretCurrentInstruction();
            }
        }

        private void ContinueFromVMBinding(int returnValue)
        {
            if (!_isRunning) {
                throw new InvalidOperationException("Cannot continue while not running");
            }
            
            _aRegister = returnValue;
            Continue();
        }

        private void InterpretCurrentInstruction()
        {
            if (_callStack.Count == 0) {
                _isRunning = false;
                _canContinue = false;
                return;
            }
            
            StackFrame top = _callStack.Peek();

            if (top.Function >= _module.Module.DefinedFunctions.Length) {
                // Bound VM function call
                VMFunction function = _module.VMFunctions[top.Function];
                int[] args = new int[top.ArgumentCount];
                for (int i = 0, ilen = top.ArgumentCount; i < ilen; ++i) {
                    args[i] = top.Memory.Read(i);
                }
                _canContinue = false;
                _callStack.Pop();
                function(args, ContinueFromVMBinding);
                return;
            }
            
            Instruction ins = _module.Module.DefinedFunctions[top.Function][top.PC];
            ++top.PC;
            InterpretInstruction(ins);
        }

        private void InterpretInstruction(Instruction ins)
        {
            switch (ins.Op) {
                case OpCode.NOOP:
                    InterpretNoOp(ins);
                    break;
                case OpCode.LOAD:
                    InterpretLoad(ins);
                    break; 
                case OpCode.LOADB:
                    InterpretLoadB(ins);
                    break;
                case OpCode.READ:
                    InterpretRead(ins);
                    break;
                case OpCode.WRITE:
                    InterpretWrite(ins);
                    break;
                case OpCode.PUSHARG:
                    InterpretPushArg(ins);
                    break;
                case OpCode.CALL:
                    InterpretCall(ins);
                    break;
                case OpCode.RETURN:
                    InterpretReturn(ins);
                    break;
                case OpCode.CMPE:
                    InterpretCmpE(ins);
                    break;
                case OpCode.CMPLT:
                    InterpretCmpLT(ins);
                    break;
                case OpCode.CMPLTE:
                    InterpretCmpLTE(ins);
                    break;
                case OpCode.JUMP:
                    InterpretJump(ins);
                    break;
                case OpCode.JUMPZ:
                    InterpretJumpZ(ins);
                    break;
                case OpCode.JUMPNZ:
                    InterpretJumpNZ(ins);
                    break;
                case OpCode.NOT:
                    InterpretNot(ins);
                    break;
                case OpCode.ADD:
                    InterpretAdd(ins);
                    break;
                case OpCode.ADDI:
                    InterpretAddI(ins);
                    break;
                case OpCode.SUB:
                    InterpretSub(ins);
                    break;
                case OpCode.SUBI:
                    InterpretSubI(ins);
                    break;
            }
        }

        private void InterpretNoOp(Instruction ins)
        {
        }

        private void InterpretLoad(Instruction ins)
        {
            _aRegister = ins.ImmediateValue;
        }

        private void InterpretLoadB(Instruction ins)
        {
            _bRegister = _aRegister;
        }

        private void InterpretRead(Instruction ins)
        {
            _aRegister = _callStack.Peek().Memory.Read(ins.ImmediateValue);
        }

        private void InterpretWrite(Instruction ins)
        {
            _callStack.Peek().Memory.Write(ins.ImmediateValue, _aRegister);
        }

        private void InterpretPushArg(Instruction ins)
        {
            _argumentStack.Add(_aRegister);
        }

        private void InterpretCall(Instruction ins)
        {
            PushCall(ins.ImmediateValue);
        }

        private void InterpretReturn(Instruction ins)
        {
            _callStack.Pop();
        }

        private void InterpretCmpE(Instruction ins)
        {
            _aRegister = _aRegister == _bRegister ? 1 : 0;
        }

        private void InterpretCmpLT(Instruction ins)
        {
            _aRegister = _aRegister < _bRegister ? 1 : 0;
        }

        private void InterpretCmpLTE(Instruction ins)
        {
            _aRegister = _aRegister <= _bRegister ? 1 : 0;
        }

        private void InterpretJump(Instruction ins)
        {
            Jump(ins.ImmediateValue);
        }

        private void InterpretJumpZ(Instruction ins)
        {
            if (_aRegister == 0) {
                Jump(ins.ImmediateValue);
            }
        }

        private void InterpretJumpNZ(Instruction ins)
        {
            if (_aRegister != 0) {
                Jump(ins.ImmediateValue);
            }
        }

        private void InterpretNot(Instruction ins)
        {
            _aRegister = _aRegister == 0 ? 1 : 0;
        }

        private void InterpretAdd(Instruction ins)
        {
            _aRegister += _bRegister;
        }

        private void InterpretAddI(Instruction ins)
        {
            _aRegister += ins.ImmediateValue;
        }

        private void InterpretSub(Instruction ins)
        {
            _aRegister -= _bRegister;
        }

        private void InterpretSubI(Instruction ins)
        {
            _aRegister -= ins.ImmediateValue;
        }
        
        private void Jump(int offset)
        {
            _callStack.Peek().PC += offset;
        }

        private void PushCall(int functionIndex)
        {
            StackFrame newFrame = new StackFrame { Function = functionIndex, ArgumentCount = _argumentStack.Count };
            for (int i = 0, ilen = _argumentStack.Count; i < ilen; ++i) {
                newFrame.Memory.Write(i, _argumentStack[i]);
            }
            _argumentStack.Clear();
            _callStack.Push(newFrame);
        }
    }
}