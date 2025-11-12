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
            children = new List<Tree<T>>();
            parent = null;
        }

        public void AddChild(Tree<T> child)
        {
            children.Add(child);
            child.parent = this;
        }

        public void SetChildren(List<Tree<T>> children)
        {
            this.children = children;
            foreach (Tree<T> child in children)
            {
                child.parent = this;
            }
        }

        public void SetParent(Tree<T> parent)
        {

            this.parent = parent;
            parent.AddChild(this);

        }

        public void SetValue(T value)
        {
            this.value = value;
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


// public class Tree<T>
//     {
//         T value;
//         List<Tree<T>> children;
//         Tree<T> parent;

//         public Tree(T value)
//         {
//             this.value = value;
//             children = new List<Tree<T>>();
//             parent = null;
//         }

//         public void AddChild(Tree<T> child)
//         {   
//             if (child == null) return;
            
//             if (child.parent != null)
//             {
//                 child.parent.RemoveChild(child);
//             }

//             children.Add(child);
//             child.parent = this;
//         }

//         public bool RemoveChild(Tree<T> child)
//         {
//             if (child == null) return false;
//             if (children.Remove(child))
//             {
//                 if (child.parent == this)
//                     child.parent = null;
//                 return true;
//             }
//             return false;
//         }

//         public void SetChildren(List<Tree<T>> newChildren)
//         {
//             // Detach current children
//             foreach (var c in new List<Tree<T>>(children))
//                 RemoveChild(c);
//             if (newChildren != null)
//             {
//                 foreach (var c in newChildren)
//                     AddChild(c);
//             }
//         }

//         public void SetParent(Tree<T> parent)
//         {
//             // Simple setter without side effect
//             this.parent = parent;
//         }

//         public void SetValue(T value)
//         {
//             this.value = value;
//         }

//         public List<Tree<T>> GetChildren()
//         {
//             return children;
//         }

//         public Tree<T> GetParent()
//         {
//             return parent;
//         }

//         public T GetValue()
//         {
//             return value;
//         }
//     }