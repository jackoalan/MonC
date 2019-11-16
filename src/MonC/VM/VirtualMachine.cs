using System;
using System.Collections.Generic;
using MonC.Bytecode;

namespace MonC.VM
{
    public class VirtualMachine : IVMBindingContext
    {
        private readonly List<StackFrame> _callStack = new List<StackFrame>();
        private readonly List<int> _argumentStack = new List<int>();
        private VMModule _module = new VMModule();
        private int _aRegister;
        private bool _canContinue;

        private Action? _breakHandler;
        private bool _isStepping;

        public bool IsRunning => _callStack.Count != 0;
        public int ReturnValue => _aRegister;
        public int CallStackFrameCount => _callStack.Count;
        
        // Make this not an event. This callback is typically only responded to once.
        public event Action? Finished;

        public void LoadModule(VMModule module)
        {
            if (IsRunning) {
                throw new InvalidOperationException("Cannot load module while running");
            }
            _module = module;
        }

        public bool Call(string functionName, IEnumerable<int> arguments, bool start = true)
        {
            if (IsRunning) {
                throw new InvalidOperationException("Cannot call function while running");
            }
            
            int functionIndex = LookupFunction(functionName);

            if (functionIndex == -1) {
                return false;
            }
            
            _argumentStack.AddRange(arguments);
            
            PushCall(functionIndex);

            if (start) {
                Continue();    
            }

            return true;
        }

        public void SetBreakHandler(Action handler)
        {
            _breakHandler = handler;
        }

        string IVMBindingContext.GetString(int id)
        {
            var strings = _module.Module.Strings;
            if (id < 0 || id >= strings.Length) {
                return "";
            }
            return strings[id];
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
        
        public void Continue()
        {
            if (_canContinue) {
                return;
            }
            
            _canContinue = true;
            
            while (_canContinue) {
                InterpretCurrentInstruction();
            }
        }

        public void SetStepping(bool isStepping)
        {
            _isStepping = isStepping;
        }

        // TODO: Rename to GetStackFrameInfo
        public StackFrameInfo GetStackFrame(int depth)
        {
            StackFrame frame = GetInternalStackFrame(depth);
            
            return new StackFrameInfo {
                Function = frame.Function,
                PC = frame.PC
            };
        }

        public StackFrameMemory GetStackFrameMemory(int depth)
        {
            StackFrame frame = GetInternalStackFrame(depth);
            return frame.Memory;
        }

        // TODO: Rename to GetStackFrame
        private StackFrame GetInternalStackFrame(int depth)
        {
            if (depth >= _callStack.Count) {
                return new StackFrame();
            }
            return _callStack[_callStack.Count - 1 - depth];
        }

        private StackFrame PeekCallStack()
        {
            return _callStack[_callStack.Count - 1];
        }

        private StackFrame PopCallStack()
        {
            int top = _callStack.Count - 1;
            StackFrame frame = _callStack[top];
            _callStack.RemoveAt(top);
            return frame;
        }

        /// <summary>
        /// Common operation for instructions which load a value from the stack based on the immediate value of the
        /// instruction. 
        /// </summary>
        private int ReadStackWithImmediateValue(Instruction ins)
        {
            return PeekCallStack().Memory.Read(ins.ImmediateValue);
        }

        private void PushCallStack(StackFrame frame)
        {
            _callStack.Add(frame);
        }

        private void Break()
        {
            _canContinue = false;
            if (_breakHandler != null) {
                _breakHandler();
            }
        }

        private void InterpretCurrentInstruction()
        {
            if (_callStack.Count == 0) {
                _canContinue = false;

                var finishedHandler = Finished;
                if (finishedHandler != null) {
                    finishedHandler();
                }
                
                return;
            }

            StackFrame top = PeekCallStack();

            if (top.BindingEnumerator != null) {
                InterpretBoundFunctionCall(top);
                return;
            }

            Instruction ins = _module.Module.DefinedFunctions[top.Function].Code[top.PC];
            ++top.PC;
            InterpretInstruction(ins);

            if (_isStepping) {
                Break();
            }
        }

        private void InterpretBoundFunctionCall(StackFrame frame)
        {
            // This method should only be called after checking that a binding enumerator exists on the given frame.
            IEnumerator<Continuation> bindingEnumerator = frame.BindingEnumerator!;
            
            if (!bindingEnumerator.MoveNext()) {
                // Function has finished
                PopFrame();
                return;
            }

            Continuation continuation = bindingEnumerator.Current;

            if (continuation.Action == ContinuationAction.CALL) {
                _argumentStack.AddRange(continuation.Arguments);
                PushCall(continuation.FunctionIndex);
                return;
            }

            if (continuation.Action == ContinuationAction.RETURN) {
                _aRegister = continuation.ReturnValue;
                PopFrame();
                return;
            }

            if (continuation.Action == ContinuationAction.YIELD) {
                _canContinue = false;
                continuation.YieldToken.OnFinished(Continue);

                // Remember: Caling Start can call the finish callback, so call Start at the very end.
                continuation.YieldToken.Start();
                
                return;
            }

            if (continuation.Action == ContinuationAction.UNWRAP) {
                StackFrame unwrapFrame = AcquireFrame();
                unwrapFrame.Function = frame.Function;
                unwrapFrame.BindingEnumerator = continuation.ToUnwrap;
                PushCallStack(unwrapFrame);
                return;
            }
            
            throw new NotImplementedException();
        }

        private void InterpretInstruction(Instruction ins)
        {
            switch (ins.Op) {
                case OpCode.NOOP:
                    InterpretNoOp(ins);
                    break;
                case OpCode.BREAK:
                    InterpretBreak(ins);
                    break;
                case OpCode.LOAD:
                    InterpretLoad(ins);
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
                case OpCode.BOOL:
                    InterpretBool(ins);
                    break;
                case OpCode.LNOT:
                    InterpretLogicalNot(ins);
                    break;
                case OpCode.ADD:
                    InterpretAdd(ins);
                    break;
                case OpCode.SUB:
                    InterpretSub(ins);
                    break;
                case OpCode.AND:
                    InterpretAnd(ins);
                    break;
                case OpCode.OR:
                    InterpretOr(ins);
                    break;
                case OpCode.MUL:
                    InterpretMul(ins);
                    break;
                case OpCode.DIV:
                    InterpretDiv(ins);
                    break;
                case OpCode.MOD:
                    InterpretMod(ins);    
                    break;
                default:
                    throw new NotImplementedException();
            }
        }

        private void InterpretNoOp(Instruction ins)
        {
        }

        private void InterpretBreak(Instruction ins)
        {
            --PeekCallStack().PC;
            Break();
        }

        private void InterpretLoad(Instruction ins)
        {
            _aRegister = ins.ImmediateValue;
        }

        private void InterpretRead(Instruction ins)
        {
            _aRegister = ReadStackWithImmediateValue(ins);
        }

        private void InterpretWrite(Instruction ins)
        {
            PeekCallStack().Memory.Write(ins.ImmediateValue, _aRegister);
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
            PopFrame();
        }

        private void InterpretCmpE(Instruction ins)
        {
            _aRegister = _aRegister == ReadStackWithImmediateValue(ins) ? 1 : 0;
        }

        private void InterpretCmpLT(Instruction ins)
        {
            _aRegister = _aRegister < ReadStackWithImmediateValue(ins) ? 1 : 0;
        }

        private void InterpretCmpLTE(Instruction ins)
        {
            _aRegister = _aRegister <= ReadStackWithImmediateValue(ins) ? 1 : 0;
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

        private void InterpretBool(Instruction ins)
        {
            _aRegister = _aRegister == 0 ? 0 : 1;
        }

        private void InterpretLogicalNot(Instruction ins)
        {
            _aRegister = _aRegister == 0 ? 1 : 0;
        }

        private void InterpretAdd(Instruction ins)
        {
            _aRegister += ReadStackWithImmediateValue(ins);
        }
        
        private void InterpretSub(Instruction ins)
        {
            _aRegister -= ReadStackWithImmediateValue(ins);
        }

        private void InterpretAnd(Instruction ins)
        {
            _aRegister &= ReadStackWithImmediateValue(ins);
        }

        private void InterpretOr(Instruction ins)
        {
            _aRegister |= ReadStackWithImmediateValue(ins);
        }

        private void InterpretMul(Instruction ins)
        {
            _aRegister *= ReadStackWithImmediateValue(ins);
        }

        private void InterpretDiv(Instruction ins)
        {
            _aRegister /= ReadStackWithImmediateValue(ins);
        }

        private void InterpretMod(Instruction ins)
        {
            _aRegister %= ReadStackWithImmediateValue(ins);
        }
        
        private void Jump(int offset)
        {
            PeekCallStack().PC += offset;
        }

        private void PushCall(int functionIndex)
        {
            StackFrame newFrame = AcquireFrame();
            newFrame.Function = functionIndex;

            if (functionIndex >= _module.Module.DefinedFunctions.Length) {
                VMEnumerable enumerable = _module.VMFunctions[functionIndex];
                int[] args = _argumentStack.ToArray();
                _argumentStack.Clear();
                newFrame.BindingEnumerator = enumerable(this, args);
                PushCallStack(newFrame);
            } else {
                for (int i = 0, ilen = _argumentStack.Count; i < ilen; ++i) {
                    newFrame.Memory.Write(i, _argumentStack[i]);
                }
                _argumentStack.Clear();
                PushCallStack(newFrame);
            }
        }
        

        private StackFrame AcquireFrame()
        {
            if (_framePool.Count > 0) {
                return _framePool.Pop();
            }
            return new StackFrame();
        }

        private void PopFrame()
        {
            StackFrame frame = PopCallStack();
            frame.BindingEnumerator = null;
            frame.PC = 0;
            _framePool.Push(frame);
        }

        private readonly Stack<StackFrame> _framePool = new Stack<StackFrame>();
    }
}