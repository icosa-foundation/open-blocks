using UnityEngine;
namespace TiltBrush.API
{
    public class HttpListener
    {
        void Start()
        {
            // This is the URL that the API will listen to
            string url = "http://localhost:40075";
            // Create a new instance of the HttpListener
            System.Net.HttpListener listener = new System.Net.HttpListener();
            // Add the URL to the listener
            listener.Prefixes.Add(url);
            // Start the listener
            listener.Start();
            // Log that the listener has started
            Debug.Log("Listening on " + url);
            // Create a new thread to listen for incoming requests
            System.Threading.Thread thread = new System.Threading.Thread(() =>
            {
                // While the listener is running
                while (listener.IsListening)
                {
                    // Get the context of the incoming request
                    System.Net.HttpListenerContext context = listener.GetContext();
                    // Get the request object
                    System.Net.HttpListenerRequest request = context.Request;
                    // Get the response object
                    System.Net.HttpListenerResponse response = context.Response;
                    // Log the request method and URL
                    Debug.Log(request.HttpMethod + " " + request.Url);
                    // Create a new StreamWriter to write the response
                    System.IO.Stream output = response.OutputStream;
                    System.IO.StreamWriter writer = new System.IO.StreamWriter(output);
                    // Write the response
                    writer.Write("Hello, world!");
                    // Close the writer
                    writer.Close();
                }
            });
            // Start the thread
            thread.Start();
        }
    }
}
