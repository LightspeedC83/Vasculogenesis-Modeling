using System;
namespace VascularGenerator.DataStructures
{
    public class Tree<T>
    {
        T value;
        List<Tree<T>> children;
        Tree<T> parent;

        public Tree(T value)
        {
            this.value = value;
            child = null;
            parent = null;
        }

        public void AddChild(Tree<T> child)
        {
            children.Add(child);
        }

        public void SetParent(Tree<T> parent)
        {
            this.parent = parent;
        }

        public List<Tree<T>> GetChildren()
        {
            return children;
        }

        public Tree<T> GetParent()
        {
            return parent;
        }

        public T GetValue()
        {
            return value;
        }
    }
}