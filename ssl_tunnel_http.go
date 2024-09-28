package main

import (
	"bufio"
	"crypto/tls"
	"encoding/binary"
	"flag"
	"fmt"
	"io"
	"log"
	"net"
	"net/http"
	"os"
	"os/signal"
	"syscall"
	"time"
)

var (
	serverIP       string
	serverPort     string
	localIP        string
	localPort      string
	clientCertPath string
	clientKeyPath  string
	logFilePath    string
	bufferSize     int
	useHTTP        bool
)

func init() {
	flag.StringVar(&serverIP, "server-ip", "", "Server IP address")
	flag.StringVar(&serverPort, "server-port", "", "Server port")
	flag.StringVar(&localIP, "local-ip", "", "Local IP address")
	flag.StringVar(&localPort, "local-port", "", "Local port")
	flag.StringVar(&clientCertPath, "cert", "", "Path to client certificate")
	flag.StringVar(&clientKeyPath, "key", "", "Path to client key")
	flag.StringVar(&logFilePath, "log", "ssl_tunnel.log", "Path to log file")
	flag.IntVar(&bufferSize, "buffer", 4096, "Buffer size for data transfer")
	flag.BoolVar(&useHTTP, "http", false, "Use HTTP/HTTPS instead of raw TCP")
}

func main() {
	flag.Parse()

	if serverIP == "" || serverPort == "" || localIP == "" || localPort == "" || clientCertPath == "" || clientKeyPath == "" {
		log.Fatal("All parameters must be provided. Use -h for help.")
	}

	// Set up logging
	logFile, err := os.OpenFile(logFilePath, os.O_APPEND|os.O_CREATE|os.O_WRONLY, 0644)
	if err != nil {
		log.Fatal(err)
	}
	defer logFile.Close()
	log.SetOutput(logFile)

	// Handle graceful shutdown
	sigChan := make(chan os.Signal, 1)
	signal.Notify(sigChan, syscall.SIGINT, syscall.SIGTERM)
	go func() {
		<-sigChan
		log.Println("Received shutdown signal. Closing tunnel...")
		os.Exit(0)
	}()

	log.Println("Starting SSL tunnel...")

	for {
		var err error
		if useHTTP {
			err = connectAndForwardHTTP()
		} else {
			err = connectAndForwardTCP()
		}
		if err != nil {
			log.Printf("Error: %v\n", err)
			log.Println("Retrying in 5 seconds...")
			time.Sleep(5 * time.Second)
		}
	}
}

func connectAndForwardTCP() error {
	cert, err := tls.LoadX509KeyPair(clientCertPath, clientKeyPath)
	if err != nil {
		return fmt.Errorf("failed to load client certificate: %v", err)
	}

	config := &tls.Config{
		Certificates:       []tls.Certificate{cert},
		InsecureSkipVerify: true,
	}
	conn, err := tls.Dial("tcp", serverIP+":"+serverPort, config)
	if err != nil {
		return fmt.Errorf("failed to connect to server: %v", err)
	}
	defer conn.Close()

	log.Println("Connected to server and SSL handshake completed.")

	key := []byte{0x00, 0x08, 0x00, 0x00, 0x00, 0x22, 0x4D, 0x00, 0x00, 0x00}
	if _, err = conn.Write(key); err != nil {
		return fmt.Errorf("failed to send initial key: %v", err)
	}
	log.Println("Sent initial key.")

	response := make([]byte, 20)
	if _, err = io.ReadFull(conn, response); err != nil {
		return fmt.Errorf("failed to receive response from server: %v", err)
	}
	log.Printf("Received response from server: %x\n", response)

	receivedServerPort := binary.BigEndian.Uint32(response[16:20])
	log.Printf("Server is listening on port: %d\n", receivedServerPort)

	localConn, err := net.Dial("tcp", localIP+":"+localPort)
	if err != nil {
		return fmt.Errorf("failed to connect to local endpoint: %v", err)
	}
	defer localConn.Close()

	log.Println("Connected to local endpoint. Forwarding traffic...")

	errChan := make(chan error, 2)
	go forward(conn, localConn, "Server -> Local", errChan)
	go forward(localConn, conn, "Local -> Server", errChan)

	return <-errChan
}

func connectAndForwardHTTP() error {
	cert, err := tls.LoadX509KeyPair(clientCertPath, clientKeyPath)
	if err != nil {
		return fmt.Errorf("failed to load client certificate: %v", err)
	}

	config := &tls.Config{
		Certificates:       []tls.Certificate{cert},
		InsecureSkipVerify: true,
	}

	transport := &http.Transport{
		TLSClientConfig: config,
	}

	client := &http.Client{
		Transport: transport,
	}

	localListener, err := net.Listen("tcp", localIP+":"+localPort)
	if err != nil {
		return fmt.Errorf("failed to start local listener: %v", err)
	}
	defer localListener.Close()

	log.Printf("Listening on %s:%s\n", localIP, localPort)

	for {
		localConn, err := localListener.Accept()
		if err != nil {
			log.Printf("Error accepting connection: %v\n", err)
			continue
		}

		go handleHTTPConnection(localConn, client)
	}
}

func handleHTTPConnection(localConn net.Conn, client *http.Client) {
	defer localConn.Close()

	reader := bufio.NewReader(localConn)
	req, err := http.ReadRequest(reader)
	if err != nil {
		log.Printf("Error reading request: %v\n", err)
		return
	}

	req.URL.Scheme = "https"
	req.URL.Host = serverIP + ":" + serverPort

	resp, err := client.Do(req)
	if err != nil {
		log.Printf("Error sending request to server: %v\n", err)
		return
	}
	defer resp.Body.Close()

	resp.Write(localConn)
	log.Printf("Forwarded request: %s %s\n", req.Method, req.URL)
}

func forward(src, dst net.Conn, direction string, errChan chan<- error) {
	buffer := make([]byte, bufferSize)
	for {
		n, err := src.Read(buffer)
		if err != nil {
			if err != io.EOF {
				errChan <- fmt.Errorf("%s read error: %v", direction, err)
			}
			break
		}
		_, err = dst.Write(buffer[:n])
		if err != nil {
			errChan <- fmt.Errorf("%s write error: %v", direction, err)
			break
		}
		log.Printf("%s: Forwarded %d bytes\n", direction, n)
	}
}