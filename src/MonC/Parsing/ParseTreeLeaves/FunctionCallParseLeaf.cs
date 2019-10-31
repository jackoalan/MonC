using System.Collections.Generic;
using System.Linq;

namespace MonC.Parsing.ParseTreeLeaves
{
    public class FunctionCallParseLeaf : IASTLeaf
    {
        public readonly IASTLeaf LHS;
        private readonly IASTLeaf[] _arguments;

        public int ArgumentCount => _arguments.Length;

        public IASTLeaf[] GetArguments()
        {
            // TODO: This sucks
            return new List<IASTLeaf>(_arguments).ToArray();
        }

        public FunctionCallParseLeaf(IASTLeaf lhs, IEnumerable<IASTLeaf> arguments)
        {
            LHS = lhs;
            _arguments = arguments.ToArray();
        }

        public void Accept(IASTLeafVisitor visitor)
        {
            if (visitor is IParseTreeLeafVisitor specificVisitor) {
                specificVisitor.VisitFunctionCall(this);
            }
        }
        
        public IASTLeaf GetArgument(int index)
        {
            return _arguments[index];
        }

        public void SetArgument(int index, IASTLeaf argument)
        {
            _arguments[index] = argument;
        }
    }
}