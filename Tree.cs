namespace VascularGenerator.DataStructures
{
    public class Tree<T>
    {
        T value;
        Tree<T> child;
        Tree<T> parent;

        public Tree(T value)
        {
            this.value = value;
        }

        public void AddChild(Tree<T> child)
        {
            this.child = child;
        }

        public void AddParent(Tree<T> parent)
        {
            this.parent = parent;
        }

        public Tree<T> GetChild()
        {
            return child;
        }

        public Tree<T> GetParent()
        {
            return parent;
        }
    }
}