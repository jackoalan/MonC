﻿using System;
using MonC.SyntaxTree.Leaves.Expressions;
using MonC.SyntaxTree.Leaves.Expressions.UnaryOperations;

namespace MonC.LLVM
{
    public struct UnaryOperationCodeGenVisitor : IUnaryOperationVisitor
    {
        private FunctionCodeGenVisitor _codeGenVisitor;

        public UnaryOperationCodeGenVisitor(FunctionCodeGenVisitor codeGenVisitor) => _codeGenVisitor = codeGenVisitor;

        public void VisitNegateUnaryOp(NegateUnaryOpLeaf leaf)
        {
            _codeGenVisitor._visitedValue = _codeGenVisitor._builder.BuildNeg(GetUnaryOperand(leaf));
        }

        public void VisitLogicalNotUnaryOp(LogicalNotUnaryOpLeaf leaf)
        {
            _codeGenVisitor._visitedValue = _codeGenVisitor.ConvertToBool(GetUnaryOperand(leaf), true);
        }

        public void VisitCastUnaryOp(CastUnaryOpLeaf leaf)
        {
            Value operand = GetUnaryOperand(leaf);
            Type destTp = _codeGenVisitor._genContext.LookupType(leaf.ToType);
            CAPI.LLVMOpcode castOp = _codeGenVisitor.GetCastOpcode(operand, destTp);
            _codeGenVisitor._visitedValue = _codeGenVisitor._builder.BuildCast(castOp, operand, destTp);
        }

        private Value GetUnaryOperand(IUnaryOperationLeaf leaf)
        {
            leaf.RHS.AcceptExpressionVisitor(_codeGenVisitor);
            Value rhs = _codeGenVisitor._visitedValue;
            if (!rhs.IsValid) {
                throw new InvalidOperationException("RHS did not produce a usable rvalue");
            }

            _codeGenVisitor.SetCurrentDebugLocation(leaf);
            return rhs;
        }
    }
}
